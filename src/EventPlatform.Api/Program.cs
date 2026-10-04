using EventPlatform.Infrastructure.Configuration;
using EventPlatform.Infrastructure.Publishing;
using EventPlatform.Infrastructure.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEventPlatformConfiguration(builder.Configuration);
builder.Services.AddSingleton<HmacSignatureValidator>();
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();
app.MapGet("/", () => Results.Ok(new { service = "EventPlatform.Api", status = "running" }));

app.Run();
