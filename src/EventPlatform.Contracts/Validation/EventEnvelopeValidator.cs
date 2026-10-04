namespace EventPlatform.Contracts.Validation;

using EventPlatform.Contracts.Events;

public static class EventEnvelopeValidator
{
    public const int MaxSubjectLength = 200;
    public const int MaxSourceLength = 100;
    public const int MaxEventTypeLength = 150;
    public const int MaxPayloadBytes = 1024 * 1024;

    public static IReadOnlyList<string> Validate(EventEnvelopeV1 envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var errors = new List<string>();

        if (envelope.EventId == Guid.Empty)
        {
            errors.Add("EventId is required.");
        }

        if (string.IsNullOrWhiteSpace(envelope.EventType))
        {
            errors.Add("EventType is required.");
        }
        else if (envelope.EventType.Length > MaxEventTypeLength)
        {
            errors.Add($"EventType length must be <= {MaxEventTypeLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Source))
        {
            errors.Add("Source is required.");
        }
        else if (envelope.Source.Length > MaxSourceLength)
        {
            errors.Add($"Source length must be <= {MaxSourceLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(envelope.SubjectId))
        {
            errors.Add("SubjectId is required.");
        }
        else if (envelope.SubjectId.Length > MaxSubjectLength)
        {
            errors.Add($"SubjectId length must be <= {MaxSubjectLength} characters.");
        }

        if (envelope.SchemaVersion != 1)
        {
            errors.Add("Only schema version 1 is supported.");
        }

        if (envelope.CorrelationId == Guid.Empty)
        {
            errors.Add("CorrelationId is required.");
        }

        if (envelope.OccurredAt == default)
        {
            errors.Add("OccurredAt is required.");
        }

        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(envelope.Payload.GetRawText());
        if (payloadBytes.Length > MaxPayloadBytes)
        {
            errors.Add($"Payload exceeds the maximum allowed size of {MaxPayloadBytes} bytes.");
        }

        return errors;
    }
}
