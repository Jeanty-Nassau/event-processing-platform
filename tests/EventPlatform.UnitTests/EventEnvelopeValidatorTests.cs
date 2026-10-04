using System.Text.Json;
using EventPlatform.Contracts.Events;
using EventPlatform.Contracts.Validation;
using NUnit.Framework;

namespace EventPlatform.UnitTests;

public class EventEnvelopeValidatorTests
{
    [Test]
    public void Validate_AllowsValidSchemaVersionOne()
    {
        var envelope = CreateValidEnvelope();

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Validate_RejectsUnsupportedSchemaVersion()
    {
        var envelope = CreateValidEnvelope() with { SchemaVersion = 99 };

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Does.Contain("Only schema version 1 is supported."));
    }

    [Test]
    public void Validate_RejectsEmptyEventType()
    {
        var envelope = CreateValidEnvelope() with { EventType = " " };

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Does.Contain("EventType is required."));
    }

    [Test]
    public void Validate_RejectsEmptySubjectId()
    {
        var envelope = CreateValidEnvelope() with { SubjectId = string.Empty };

        var result = EventEnvelopeValidator.Validate(envelope);

        Assert.That(result, Does.Contain("SubjectId is required."));
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
