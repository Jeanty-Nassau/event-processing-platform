using System.Text.Json;
using EventPlatform.Contracts.Events;
using EventPlatform.Contracts.Validation;
using EventPlatform.Infrastructure.Observability;
using EventPlatform.Infrastructure.Publishing;
using EventPlatform.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace EventPlatform.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class EventsController : ControllerBase
{
    private const int MaxRequestBytes = 2 * 1024 * 1024;

    private readonly HmacSignatureValidator _signatureValidator;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        HmacSignatureValidator signatureValidator,
        IEventPublisher eventPublisher,
        ILogger<EventsController> logger)
    {
        _signatureValidator = signatureValidator;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    [HttpPost("events")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> PostAsync(CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            EventPlatformTelemetry.RejectedEvents.Add(1, new("reason", "request_too_large"));
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new { error = "Request body is too large." });
        }

        await using var buffer = new MemoryStream(
            Request.ContentLength is > 0 and <= MaxRequestBytes
                ? (int)Request.ContentLength.Value
                : 0);

        await Request.Body.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length == 0)
        {
            EventPlatformTelemetry.RejectedEvents.Add(1, new("reason", "empty_body"));
            return BadRequest(new { error = "Request body is required." });
        }

        if (buffer.Length > MaxRequestBytes)
        {
            EventPlatformTelemetry.RejectedEvents.Add(1, new("reason", "request_too_large"));
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new { error = "Request body is too large." });
        }

        var bodyBytes = buffer.ToArray();
        var suppliedSignature = Request.Headers["X-Signature"].ToString();

        if (string.IsNullOrWhiteSpace(suppliedSignature) ||
            !_signatureValidator.IsValid(bodyBytes, suppliedSignature))
        {
            EventPlatformTelemetry.RejectedEvents.Add(1, new("reason", "invalid_signature"));
            return Unauthorized(new { error = "Invalid signature." });
        }

        EventEnvelopeV1? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelopeV1>(
                bodyBytes,
                SerializerOptions);
        }
        catch (JsonException ex)
        {
            EventPlatformTelemetry.RejectedEvents.Add(1, new("reason", "invalid_json"));
            _logger.LogWarning(ex, "Invalid JSON event payload received");
            return BadRequest(new { error = "Invalid JSON." });
        }

        if (envelope is null)
        {
            EventPlatformTelemetry.RejectedEvents.Add(1, new("reason", "null_event"));
            return BadRequest(new { error = "Event payload is required." });
        }

        using var activity = EventPlatformTelemetry.ActivitySource.StartActivity("event.ingest");
        activity?.SetTag("event.id", envelope.EventId);
        activity?.SetTag("event.type", envelope.EventType);

        var validationErrors = EventEnvelopeValidator.Validate(envelope);
        if (validationErrors.Count > 0)
        {
            EventPlatformTelemetry.RejectedEvents.Add(1, new("reason", "validation"));
            return UnprocessableEntity(new
            {
                error = "Event validation failed.",
                details = validationErrors
            });
        }

        var publishResult = await _eventPublisher.PublishAsync(envelope, cancellationToken);
        if (!publishResult.IsSuccess)
        {
            EventPlatformTelemetry.RejectedEvents.Add(1, new("reason", "broker_unavailable"));
            _logger.LogWarning(
                "Kafka publish failed for event {EventId} correlation {CorrelationId}",
                envelope.EventId,
                envelope.CorrelationId);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "Event broker is unavailable." });
        }

        EventPlatformTelemetry.IngestedEvents.Add(1, new("event.type", envelope.EventType));
        _logger.LogInformation(
            "Accepted event {EventId} for subject {SubjectId} correlation {CorrelationId}; partition {Partition} offset {Offset}",
            envelope.EventId,
            envelope.SubjectId,
            envelope.CorrelationId,
            publishResult.Partition,
            publishResult.Offset);

        return Accepted(new
        {
            eventId = envelope.EventId,
            correlationId = envelope.CorrelationId,
            status = "accepted"
        });
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };
}
