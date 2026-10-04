using EventPlatform.Infrastructure.Configuration;
using EventPlatform.Infrastructure.Observability;
using EventPlatform.Infrastructure.Publishing;
using EventPlatform.RetryDispatcher;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddKafkaConfiguration(builder.Configuration)
    .AddPostgresConfiguration(builder.Configuration)
    .AddProcessingConfiguration(builder.Configuration)
    .AddEventPlatformPersistence();

builder.Services.AddEventPlatformMessaging();
builder.Services.AddEventPlatformTelemetry("EventPlatform.RetryDispatcher");
builder.Services.AddHostedService<RetryDispatcherWorker>();

var host = builder.Build();
await host.RunAsync();
