using System.Text;
using System.Text.Json;
using EventPlatform.Contracts.Events;
using EventPlatform.Contracts.Validation;
using EventPlatform.Infrastructure.Publishing;
using EventPlatform.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace EventPlatform.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class EventsController : ControllerBase
{
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
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> PostAsync(CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > EventEnvelopeValidator.MaxPayloadBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "Payload too large." });
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return BadRequest(new { error = "Request body is required." });
        }

        var suppliedSignature = Request.Headers["X-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(suppliedSignature) || !_signatureValidator.Validate(payload, suppliedSignature))
        {
            return Unauthorized(new { error = "Invalid signature." });
        }

        EventEnvelopeV1? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelopeV1>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON event payload received");
            return UnprocessableEntity(new { error = "Invalid event payload." });
        }

        if (envelope is null)
        {
            return BadRequest(new { error = "Event payload is required." });
        }

        var validationErrors = EventEnvelopeValidator.Validate(envelope);
        if (validationErrors.Count > 0)
        {
            return UnprocessableEntity(new { error = "Event validation failed.", details = validationErrors });
        }

        var publishResult = await _eventPublisher.PublishAsync(envelope, cancellationToken);
        if (!publishResult.IsSuccess)
        {
            _logger.LogWarning(
                "Kafka publish failed for event {EventId} correlation {CorrelationId}",
                envelope.EventId,
                envelope.CorrelationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Event broker is unavailable." });
        }

        _logger.LogInformation(
            "Accepted event {EventId} for subject {SubjectId} correlation {CorrelationId}",
            envelope.EventId,
            envelope.SubjectId,
            envelope.CorrelationId);

        return Accepted(new
        {
            eventId = envelope.EventId,
            correlationId = envelope.CorrelationId,
            status = "accepted"
        });
    }
}
