using System.Text.Json;
using EventPlatform.Contracts.Events;
using EventPlatform.Contracts.Validation;
using NUnit.Framework;

namespace EventPlatform.UnitTests;

public class EventEnvelopeValidatorAdditionalTests
{
    [Test]
    public void Validate_RejectsEmptyEventId()
    {
        var envelope = CreateValidEnvelope() with { EventId = Guid.Empty };

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Does.Contain("EventId is required."));
    }

    [Test]
    public void Validate_RejectsEmptyCorrelationId()
    {
        var envelope = CreateValidEnvelope() with { CorrelationId = Guid.Empty };

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Does.Contain("CorrelationId is required."));
    }

    [Test]
    public void Validate_RejectsEmptySource()
    {
        var envelope = CreateValidEnvelope() with { Source = string.Empty };

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Does.Contain("Source is required."));
    }

    [Test]
    public void Validate_RejectsSourceWhenTooLong()
    {
        var envelope = CreateValidEnvelope() with { Source = new string('x', EventEnvelopeValidator.MaxSourceLength + 1) };

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Does.Contain($"Source length must be <= {EventEnvelopeValidator.MaxSourceLength} characters."));
    }

    [Test]
    public void Validate_RejectsEventTypeWhenTooLong()
    {
        var envelope = CreateValidEnvelope() with { EventType = new string('y', EventEnvelopeValidator.MaxEventTypeLength + 1) };

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Does.Contain($"EventType length must be <= {EventEnvelopeValidator.MaxEventTypeLength} characters."));
    }

    [Test]
    public void Validate_RejectsSubjectIdWhenTooLong()
    {
        var envelope = CreateValidEnvelope() with { SubjectId = new string('z', EventEnvelopeValidator.MaxSubjectLength + 1) };

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Does.Contain($"SubjectId length must be <= {EventEnvelopeValidator.MaxSubjectLength} characters."));
    }

    [Test]
    public void Validate_RejectsDefaultOccurredAt()
    {
        var envelope = CreateValidEnvelope() with { OccurredAt = default };

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Does.Contain("OccurredAt is required."));
    }

    [Test]
    public void Validate_RejectsUndefinedPayload()
    {
        var envelope = CreateValidEnvelope();
        var undefinedPayload = new EventEnvelopeV1
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            Source = envelope.Source,
            SubjectId = envelope.SubjectId,
            SchemaVersion = envelope.SchemaVersion,
            OccurredAt = envelope.OccurredAt,
            CorrelationId = envelope.CorrelationId,
            Payload = default(JsonElement)
        };

        var result = EventEnvelopeValidator.Validate(undefinedPayload);

        Assert.That(result, Does.Contain("Payload is required."));
    }

    [Test]
    public void Validate_RejectsNullPayload()
    {
        var envelope = CreateValidEnvelope();
        var nullPayload = new EventEnvelopeV1
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            Source = envelope.Source,
            SubjectId = envelope.SubjectId,
            SchemaVersion = envelope.SchemaVersion,
            OccurredAt = envelope.OccurredAt,
            CorrelationId = envelope.CorrelationId,
            Payload = JsonDocument.Parse("null").RootElement.Clone()
        };

        var result = EventEnvelopeValidator.Validate(nullPayload);

        Assert.That(result, Does.Contain("Payload is required."));
    }

    [Test]
    public void Validate_RejectsPayloadOverLimit()
    {
        var envelope = CreateValidEnvelope() with
        {
            Payload = JsonDocument.Parse($"{{\"value\":\"{new string('a', EventEnvelopeValidator.MaxPayloadBytes + 1)}\"}}").RootElement.Clone()
        };

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Does.Contain($"Payload exceeds the maximum allowed size of {EventEnvelopeValidator.MaxPayloadBytes} bytes."));
    }

    private static EventEnvelopeV1 CreateValidEnvelope()
    {
        return new EventEnvelopeV1
        {
            EventId = Guid.NewGuid(),
            EventType = "demo.work.requested",
            Source = "event-generator",
            SubjectId = "subject-1287",
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = JsonDocument.Parse("{\"operation\":\"transform\",\"value\":42}").RootElement.Clone()
        };
    }
}
