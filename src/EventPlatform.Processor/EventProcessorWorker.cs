using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Dapper;
using EventPlatform.Contracts.Events;
using EventPlatform.Contracts.Validation;
using EventPlatform.Infrastructure.Configuration;
using EventPlatform.Infrastructure.Observability;
using EventPlatform.Infrastructure.Publishing;
using EventPlatform.Infrastructure.Retry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EventPlatform.Processor;

public sealed class EventProcessorWorker : BackgroundService
{
    private const int MaxDeadLetterPayloadChars = 65_536;

    private readonly KafkaOptions _kafkaOptions;
    private readonly ProcessingOptions _processingOptions;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IDeadLetterPublisher _deadLetterPublisher;
    private readonly IRetryBackoffStrategy _retryBackoff;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EventProcessorWorker> _logger;

    public EventProcessorWorker(
        KafkaOptions kafkaOptions,
        ProcessingOptions processingOptions,
        NpgsqlDataSource dataSource,
        IDeadLetterPublisher deadLetterPublisher,
        IRetryBackoffStrategy retryBackoff,
        TimeProvider timeProvider,
        ILogger<EventProcessorWorker> logger)
    {
        _kafkaOptions = kafkaOptions;
        _processingOptions = processingOptions;
        _dataSource = dataSource;
        _deadLetterPublisher = deadLetterPublisher;
        _retryBackoff = retryBackoff;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = _kafkaOptions.ConsumerGroup,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = false,
            SessionTimeoutMs = 10_000,
            HeartbeatIntervalMs = 3_000,
            MaxPollIntervalMs = 300_000
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => _logger.LogError("Kafka consumer error: {Reason}", error.Reason))
            .Build();

        consumer.Subscribe(_kafkaOptions.EventsTopic);
        _logger.LogInformation(
            "Processor subscribed to {Topic} using consumer group {ConsumerGroup}",
            _kafkaOptions.EventsTopic,
            _kafkaOptions.ConsumerGroup);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;

                try
                {
                    result = consumer.Consume(stoppingToken);
                    if (result is null)
                    {
                        continue;
                    }

                    var safelyHandled = await HandleMessageAsync(result, stoppingToken);
                    if (safelyHandled)
                    {
                        consumer.StoreOffset(result);
                        consumer.Commit(result);
                    }
                    else
                    {
                        consumer.Seek(result.TopicPartitionOffset);
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (NpgsqlException ex)
                {
                    _logger.LogError(
                        ex,
                        "PostgreSQL is unavailable while handling topic {Topic} partition {Partition} offset {Offset}; offset will not be committed",
                        result?.Topic,
                        result?.Partition.Value,
                        result?.Offset.Value);

                    if (result is not null)
                    {
                        consumer.Seek(result.TopicPartitionOffset);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume failed for topic {Topic}", _kafkaOptions.EventsTopic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Unexpected processor failure at topic {Topic} partition {Partition} offset {Offset}; offset will not be committed",
                        result?.Topic,
                        result?.Partition.Value,
                        result?.Offset.Value);

                    if (result is not null)
                    {
                        consumer.Seek(result.TopicPartitionOffset);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
        finally
        {
            _logger.LogInformation("Closing Kafka consumer and leaving consumer group");
            consumer.Close();
        }
    }

    private async Task<bool> HandleMessageAsync(
        ConsumeResult<string, string> result,
        CancellationToken cancellationToken)
    {
        using var activity = EventPlatformTelemetry.ActivitySource.StartActivity("event.consume");
        activity?.SetTag("messaging.destination.name", result.Topic);
        activity?.SetTag("messaging.kafka.partition", result.Partition.Value);
        activity?.SetTag("messaging.kafka.offset", result.Offset.Value);

        var retryAttempt = GetRetryAttempt(result.Message.Headers);
        EventEnvelopeV1? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelopeV1>(
                result.Message.Value,
                SerializerOptions);
        }
        catch (JsonException)
        {
            return await DeadLetterAsync(
                result,
                null,
                "permanent",
                "malformed_json",
                retryAttempt,
                cancellationToken);
        }

        if (envelope is null)
        {
            return await DeadLetterAsync(
                result,
                null,
                "permanent",
                "null_event",
                retryAttempt,
                cancellationToken);
        }

        activity?.SetTag("event.id", envelope.EventId);
        activity?.SetTag("event.type", envelope.EventType);

        var validationErrors = EventEnvelopeValidator.Validate(envelope);
        if (validationErrors.Count > 0)
        {
            _logger.LogWarning(
                "Event {EventId} failed validation: {ValidationErrors}",
                envelope.EventId,
                string.Join("; ", validationErrors));

            return await DeadLetterAsync(
                result,
                envelope,
                "permanent",
                "validation_failed",
                retryAttempt,
                cancellationToken);
        }

        if (!string.Equals(envelope.EventType, "demo.work.requested", StringComparison.Ordinal))
        {
            return await DeadLetterAsync(
                result,
                envelope,
                "permanent",
                "unsupported_event_type",
                retryAttempt,
                cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var processingResult = ProcessDemoWork(envelope, retryAttempt);
            var inserted = await TryPersistProcessedEventAsync(
                envelope,
                processingResult,
                cancellationToken);

            if (inserted)
            {
                EventPlatformTelemetry.ProcessedEvents.Add(1, new("event.type", envelope.EventType));
                _logger.LogInformation(
                    "Processed event {EventId} subject {SubjectId} type {EventType} at retry attempt {RetryAttempt}",
                    envelope.EventId,
                    envelope.SubjectId,
                    envelope.EventType,
                    retryAttempt);
            }
            else
            {
                EventPlatformTelemetry.DuplicateEvents.Add(1, new("event.type", envelope.EventType));
                _logger.LogInformation(
                    "Duplicate event {EventId} already processed; treating as idempotent success",
                    envelope.EventId);
            }

            return true;
        }
        catch (PermanentProcessingException ex)
        {
            EventPlatformTelemetry.FailedEvents.Add(1, new("failure.category", "permanent"));
            _logger.LogWarning(ex, "Permanent processing failure for event {EventId}", envelope.EventId);

            return await DeadLetterAsync(
                result,
                envelope,
                "permanent",
                "invalid_demo_payload",
                retryAttempt,
                cancellationToken);
        }
        catch (OverflowException ex)
        {
            EventPlatformTelemetry.FailedEvents.Add(1, new("failure.category", "permanent"));
            _logger.LogWarning(ex, "Numeric overflow while processing event {EventId}", envelope.EventId);

            return await DeadLetterAsync(
                result,
                envelope,
                "permanent",
                "numeric_overflow",
                retryAttempt,
                cancellationToken);
        }
        catch (TransientProcessingException ex)
        {
            EventPlatformTelemetry.FailedEvents.Add(1, new("failure.category", "transient"));

            if (retryAttempt >= _processingOptions.MaxRetryAttempts)
            {
                return await DeadLetterAsync(
                    result,
                    envelope,
                    "exhausted",
                    "retry_exhausted",
                    retryAttempt,
                    cancellationToken);
            }

            var nextAttempt = retryAttempt + 1;
            var delay = _retryBackoff.GetDelay(nextAttempt);
            var nextAttemptAt = _timeProvider.GetUtcNow().Add(delay);

            await ScheduleRetryAsync(
                result,
                envelope,
                nextAttempt,
                nextAttemptAt,
                ex.Message,
                cancellationToken);

            EventPlatformTelemetry.ScheduledRetries.Add(1, new("event.type", envelope.EventType));
            _logger.LogWarning(
                ex,
                "Scheduled retry {Attempt} for event {EventId} at {NextAttemptAt}",
                nextAttempt,
                envelope.EventId,
                nextAttemptAt);

            return true;
        }
        finally
        {
            stopwatch.Stop();
            EventPlatformTelemetry.ProcessingDuration.Record(
                stopwatch.Elapsed.TotalSeconds,
                new("event.type", envelope.EventType));
        }
    }

    private static object ProcessDemoWork(EventEnvelopeV1 envelope, int retryAttempt)
    {
        if (envelope.Payload.ValueKind != JsonValueKind.Object)
        {
            throw new PermanentProcessingException("Payload must be a JSON object.");
        }

        if (!envelope.Payload.TryGetProperty("operation", out var operationElement) ||
            !string.Equals(operationElement.GetString(), "transform", StringComparison.Ordinal))
        {
            throw new PermanentProcessingException("Only the transform operation is supported.");
        }

        if (!envelope.Payload.TryGetProperty("value", out var valueElement) ||
            !valueElement.TryGetInt32(out var value))
        {
            throw new PermanentProcessingException("Payload value must be a 32-bit integer.");
        }

        if (envelope.Payload.TryGetProperty("failUntilAttempt", out var failElement) &&
            failElement.TryGetInt32(out var failUntilAttempt) &&
            retryAttempt < failUntilAttempt)
        {
            throw new TransientProcessingException(
                $"Simulated transient failure until retry attempt {failUntilAttempt}.");
        }

        return new
        {
            operation = "transform",
            inputValue = value,
            outputValue = checked(value * 2)
        };
    }

    private async Task<bool> TryPersistProcessedEventAsync(
        EventEnvelopeV1 envelope,
        object processingResult,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO processed_events (
                event_id,
                event_type,
                subject_id,
                source,
                schema_version,
                correlation_id,
                occurred_at,
                received_at,
                processed_at,
                status,
                result)
            VALUES (
                @EventId,
                @EventType,
                @SubjectId,
                @Source,
                @SchemaVersion,
                @CorrelationId,
                @OccurredAt,
                @ReceivedAt,
                @ProcessedAt,
                'processed',
                CAST(@Result AS jsonb))
            ON CONFLICT (event_id) DO NOTHING
            RETURNING event_id;
            """;

        var now = _timeProvider.GetUtcNow();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                envelope.EventId,
                envelope.EventType,
                envelope.SubjectId,
                envelope.Source,
                envelope.SchemaVersion,
                envelope.CorrelationId,
                envelope.OccurredAt,
                ReceivedAt = now,
                ProcessedAt = now,
                Result = JsonSerializer.Serialize(processingResult)
            },
            cancellationToken: cancellationToken);

        var inserted = await connection.QuerySingleOrDefaultAsync<Guid?>(command);
        return inserted.HasValue;
    }

    private async Task ScheduleRetryAsync(
        ConsumeResult<string, string> result,
        EventEnvelopeV1 envelope,
        int nextAttempt,
        DateTimeOffset nextAttemptAt,
        string lastError,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO retry_schedule (
                id,
                event_id,
                original_topic,
                original_partition,
                original_offset,
                event_key,
                payload,
                attempt,
                next_attempt_at,
                failure_category,
                error_code,
                last_error,
                created_at,
                updated_at)
            VALUES (
                @Id,
                @EventId,
                @OriginalTopic,
                @OriginalPartition,
                @OriginalOffset,
                @EventKey,
                CAST(@Payload AS jsonb),
                @Attempt,
                @NextAttemptAt,
                'transient',
                'processing_transient',
                @LastError,
                @Now,
                @Now)
            ON CONFLICT (event_id) DO UPDATE SET
                attempt = EXCLUDED.attempt,
                next_attempt_at = EXCLUDED.next_attempt_at,
                last_error = EXCLUDED.last_error,
                updated_at = EXCLUDED.updated_at;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = Guid.NewGuid(),
                envelope.EventId,
                OriginalTopic = result.Topic,
                OriginalPartition = result.Partition.Value,
                OriginalOffset = result.Offset.Value,
                EventKey = result.Message.Key ?? envelope.SubjectId,
                Payload = result.Message.Value,
                Attempt = nextAttempt,
                NextAttemptAt = nextAttemptAt,
                LastError = Truncate(lastError, 1000),
                Now = _timeProvider.GetUtcNow()
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private async Task<bool> DeadLetterAsync(
        ConsumeResult<string, string> result,
        EventEnvelopeV1? envelope,
        string failureCategory,
        string reasonCode,
        int attemptCount,
        CancellationToken cancellationToken)
    {
        var deadLetter = new DeadLetterEnvelopeV1
        {
            EventId = envelope?.EventId,
            EventType = envelope?.EventType,
            FailureCategory = failureCategory,
            ReasonCode = reasonCode,
            AttemptCount = attemptCount,
            OriginalTopic = result.Topic,
            OriginalPartition = result.Partition.Value,
            OriginalOffset = result.Offset.Value,
            FailedAt = _timeProvider.GetUtcNow(),
            RawPayload = Truncate(result.Message.Value, MaxDeadLetterPayloadChars)
        };

        var key = result.Message.Key ?? envelope?.SubjectId ?? "unknown";
        var published = await _deadLetterPublisher.PublishAsync(deadLetter, key, cancellationToken);

        if (!published.IsSuccess)
        {
            _logger.LogError(
                "Failed to publish source topic {Topic} partition {Partition} offset {Offset} to DLQ; source offset will not be committed",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value);
            return false;
        }

        EventPlatformTelemetry.DeadLetteredEvents.Add(1, new("failure.category", failureCategory));
        _logger.LogWarning(
            "Dead-lettered event {EventId} from partition {Partition} offset {Offset}; reason {ReasonCode}",
            envelope?.EventId,
            result.Partition.Value,
            result.Offset.Value,
            reasonCode);

        return true;
    }

    private static int GetRetryAttempt(Headers? headers)
    {
        if (headers is null)
        {
            return 0;
        }

        var retryHeader = headers.LastOrDefault(header =>
            string.Equals(header.Key, "retry-attempt", StringComparison.Ordinal));

        if (retryHeader is null)
        {
            return 0;
        }

        var bytes = retryHeader.GetValueBytes();
        return int.TryParse(Encoding.UTF8.GetString(bytes), NumberStyles.None, CultureInfo.InvariantCulture, out var attempt)
            ? Math.Max(0, attempt)
            : 0;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private sealed class TransientProcessingException : Exception
    {
        public TransientProcessingException(string message) : base(message)
        {
        }
    }

    private sealed class PermanentProcessingException : Exception
    {
        public PermanentProcessingException(string message) : base(message)
        {
        }
    }
}
