using EventPlatform.Infrastructure.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddEventPlatformConfiguration(builder.Configuration);
builder.Services.AddHealthChecks();

var host = builder.Build();

await host.RunAsync();
