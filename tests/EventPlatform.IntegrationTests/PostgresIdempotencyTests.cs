using Dapper;
using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace EventPlatform.IntegrationTests;

public sealed class PostgresIdempotencyTests
{
    private PostgreSqlContainer _postgres = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16.4")
            .WithDatabase("eventplatform")
            .WithUsername("eventplatform")
            .WithPassword("integration-password")
            .Build();

        await _postgres.StartAsync();

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await connection.ExecuteAsync("""
            CREATE TABLE processed_events (
                event_id UUID PRIMARY KEY,
                status VARCHAR(50) NOT NULL
            );
            """);
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Test]
    public async Task DuplicateEventId_CreatesOneLogicalRecord()
    {
        var eventId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO processed_events(event_id, status)
            VALUES (@EventId, 'processed')
            ON CONFLICT(event_id) DO NOTHING
            RETURNING event_id;
            """;

        var first = await connection.QuerySingleOrDefaultAsync<Guid?>(sql, new { EventId = eventId });
        var second = await connection.QuerySingleOrDefaultAsync<Guid?>(sql, new { EventId = eventId });
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM processed_events WHERE event_id = @EventId;",
            new { EventId = eventId });

        Assert.That(first, Is.EqualTo(eventId));
        Assert.That(second, Is.Null);
        Assert.That(count, Is.EqualTo(1));
    }
}
