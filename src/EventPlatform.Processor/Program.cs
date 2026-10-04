using EventPlatform.Infrastructure.Configuration;
using EventPlatform.Infrastructure.Observability;
using EventPlatform.Infrastructure.Publishing;
using EventPlatform.Infrastructure.Retry;
using EventPlatform.Processor;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddKafkaConfiguration(builder.Configuration)
    .AddPostgresConfiguration(builder.Configuration)
    .AddProcessingConfiguration(builder.Configuration)
    .AddEventPlatformPersistence();

builder.Services.AddEventPlatformMessaging();
builder.Services.AddEventPlatformRetryPolicy();
builder.Services.AddEventPlatformTelemetry("EventPlatform.Processor");
builder.Services.AddHostedService<EventProcessorWorker>();

var host = builder.Build();
await host.RunAsync();
