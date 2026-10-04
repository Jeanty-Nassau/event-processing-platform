using EventPlatform.Infrastructure.Configuration;
using EventPlatform.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EventSecurityOptions>(builder.Configuration.GetSection(EventSecurityOptions.SectionName));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<PostgresOptions>(builder.Configuration.GetSection(PostgresOptions.SectionName));

builder.Services.AddSingleton<HmacSignatureValidator>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapGet("/", () => Results.Ok(new { service = "EventPlatform.Api", status = "running" }));

app.Run();
