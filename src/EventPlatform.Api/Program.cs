using EventPlatform.Infrastructure.Configuration;
using EventPlatform.Infrastructure.Health;
using EventPlatform.Infrastructure.Observability;
using EventPlatform.Infrastructure.Publishing;
using EventPlatform.Infrastructure.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEventSecurityConfiguration(builder.Configuration)
    .AddKafkaConfiguration(builder.Configuration);

builder.Services.AddSingleton<HmacSignatureValidator>();
builder.Services.AddEventPlatformMessaging();
builder.Services.AddEventPlatformTelemetry("EventPlatform.Api", instrumentAspNetCore: true);
builder.Services.AddControllers();

builder.Services.AddHealthChecks()
    .AddCheck<KafkaReadinessHealthCheck>("kafka", tags: ["ready"]);

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapControllers();
app.MapGet("/", () => Results.Ok(new { service = "EventPlatform.Api", status = "running" }));

app.Run();

public partial class Program;
