using EventPlatform.Infrastructure.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<EventSecurityOptions>(builder.Configuration.GetSection(EventSecurityOptions.SectionName));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<PostgresOptions>(builder.Configuration.GetSection(PostgresOptions.SectionName));

builder.Services.AddHealthChecks();

var host = builder.Build();

await host.RunAsync();
