using System.Globalization;
using Confluent.Kafka;
using Dapper;
using EventPlatform.Infrastructure.Configuration;
using EventPlatform.Infrastructure.Observability;
using EventPlatform.Infrastructure.Publishing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EventPlatform.RetryDispatcher;

public sealed class RetryDispatcherWorker : BackgroundService
{
    private readonly ProcessingOptions _processingOptions;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IKafkaMessagePublisher _publisher;
    private readonly ILogger<RetryDispatcherWorker> _logger;

    public RetryDispatcherWorker(
        ProcessingOptions processingOptions,
        NpgsqlDataSource dataSource,
        IKafkaMessagePublisher publisher,
        ILogger<RetryDispatcherWorker> logger)
    {
        _processingOptions = processingOptions;
        _dataSource = dataSource;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Retry dispatcher started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dispatchedAny = false;

                for (var i = 0; i < _processingOptions.RetryDispatcherBatchSize; i++)
                {
                    if (!await TryDispatchOneAsync(stoppingToken))
                    {
                        break;
                    }

                    dispatchedAny = true;
                }

                if (!dispatchedAny)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(_processingOptions.RetryPollIntervalMs),
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (NpgsqlException ex)
            {
                _logger.LogError(ex, "PostgreSQL unavailable while dispatching retries");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected retry dispatcher failure");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        _logger.LogInformation("Retry dispatcher stopped");
    }

    private async Task<bool> TryDispatchOneAsync(CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT
                id AS "Id",
                event_id AS "EventId",
                original_topic AS "OriginalTopic",
                event_key AS "EventKey",
                payload::text AS "Payload",
                attempt AS "Attempt"
            FROM retry_schedule
            WHERE next_attempt_at <= NOW()
            ORDER BY next_attempt_at
            FOR UPDATE SKIP LOCKED
            LIMIT 1;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var record = await connection.QuerySingleOrDefaultAsync<RetryRecord>(
            new CommandDefinition(
                selectSql,
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (record is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var headers = new Dictionary<string, string>
        {
            ["event-id"] = record.EventId.ToString(),
            ["retry-attempt"] = record.Attempt.ToString(CultureInfo.InvariantCulture)
        };

        var publishResult = await _publisher.PublishAsync(
            record.OriginalTopic,
            record.EventKey,
            record.Payload,
            headers,
            cancellationToken);

        if (!publishResult.IsSuccess)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(
                "Retry publish failed for event {EventId}; retry row remains durable",
                record.EventId);
            return false;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM retry_schedule WHERE id = @Id;",
                new { record.Id },
                transaction,
                cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        EventPlatformTelemetry.DispatchedRetries.Add(1);
        _logger.LogInformation(
            "Republished retry attempt {Attempt} for event {EventId}",
            record.Attempt,
            record.EventId);

        return true;
    }

    private sealed class RetryRecord
    {
        public Guid Id { get; init; }

        public Guid EventId { get; init; }

        public string OriginalTopic { get; init; } = string.Empty;

        public string EventKey { get; init; } = string.Empty;

        public string Payload { get; init; } = string.Empty;

        public int Attempt { get; init; }
    }
}
