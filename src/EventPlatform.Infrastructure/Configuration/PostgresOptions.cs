namespace EventPlatform.Infrastructure.Configuration;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    public string ConnectionString { get; init; } = "Host=localhost;Database=eventplatform;Username=postgres;Password=postgres";
}
