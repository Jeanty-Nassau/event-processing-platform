namespace EventPlatform.Infrastructure.Configuration;

public sealed class EventSecurityOptions
{
    public const string SectionName = "EventSecurity";

    public string SigningSecret { get; init; } = string.Empty;
}
