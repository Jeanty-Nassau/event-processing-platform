using System.Text.Json;
using Confluent.Kafka;
using Dapper;
using EventPlatform.Contracts.Events;
using EventPlatform.Contracts.Validation;
using EventPlatform.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EventPlatform.Processor;

public sealed class EventProcessorWorker : BackgroundService
{
    private readonly KafkaOptions _kafkaOptions;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<EventProcessorWorker> _logger;

    public EventProcessorWorker(
        KafkaOptions kafkaOptions,
        NpgsqlDataSource dataSource,
        ILogger<EventProcessorWorker> logger)
    {
        _kafkaOptions = kafkaOptions;
        _dataSource = dataSource;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = _kafkaOptions.ConsumerGroup,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = true,
            EnablePartitionEof = true,
            SessionTimeoutMs = 10_000,
            HeartbeatIntervalMs = 3_000,
            MaxPollIntervalMs = 60_000,
            SecurityProtocol = SecurityProtocol.Plaintext
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => _logger.LogError("Kafka consumer error: {Reason}", error.Reason))
            .Build();

        consumer.Subscribe(_kafkaOptions.EventsTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result == null)
                {
                    continue;
                }

                await ProcessMessageAsync(result, stoppingToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume failed for topic {Topic}", _kafkaOptions.EventsTopic);
            }
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
    {
        var key = result.Message.Key ?? "";
        EventEnvelopeV1? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelopeV1>(result.Message.Value, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Malformed JSON encountered while consuming message from partition {Partition} offset {Offset}", result.Partition.Value, result.Offset.Value);
            return;
        }

        if (envelope is null)
        {
            _logger.LogWarning("Consumed null event from partition {Partition} offset {Offset}", result.Partition.Value, result.Offset.Value);
            return;
        }

        var validationErrors = EventEnvelopeValidator.Validate(envelope);
        if (validationErrors.Count > 0)
        {
            _logger.LogWarning(
                "Rejected event {EventId} due to validation errors: {Errors}",
                envelope.EventId,
                string.Join("; ", validationErrors));
            return;
        }

        var persisted = await TryPersistProcessedEventAsync(envelope, cancellationToken);
        if (persisted)
        {
            _logger.LogInformation(
                "Processed event {EventId} subject {SubjectId} type {EventType}",
                envelope.EventId,
                envelope.SubjectId,
                envelope.EventType);
            return;
        }

        _logger.LogInformation(
            "Duplicate event {EventId} already processed; treating as idempotent success",
            envelope.EventId);
    }

    private async Task<bool> TryPersistProcessedEventAsync(EventEnvelopeV1 envelope, CancellationToken cancellationToken)
    {
        var payload = new
        {
            operation = "transform",
            inputValue = 42,
            outputValue = 84
        };

        var sql = @"
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
                NOW(),
                NOW(),
                'processed',
                @Result::jsonb)
            ON CONFLICT (event_id) DO NOTHING
            RETURNING event_id;";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var inserted = await connection.QuerySingleOrDefaultAsync<Guid?>(sql, new
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            SubjectId = envelope.SubjectId,
            Source = envelope.Source,
            SchemaVersion = envelope.SchemaVersion,
            CorrelationId = envelope.CorrelationId,
            OccurredAt = envelope.OccurredAt,
            Result = JsonSerializer.Serialize(payload)
        }, cancellationToken: cancellationToken);

        return inserted.HasValue;
    }
}
