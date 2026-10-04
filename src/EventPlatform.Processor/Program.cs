using EventPlatform.Infrastructure.Configuration;
using EventPlatform.Processor;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddEventPlatformConfiguration(builder.Configuration);
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<KafkaOptions>();
    var postgresOptions = sp.GetRequiredService<PostgresOptions>();

    var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(postgresOptions.ConnectionString);
    return dataSourceBuilder.Build();
});
builder.Services.AddHostedService<EventProcessorWorker>();
builder.Services.AddHealthChecks();

var host = builder.Build();

await host.RunAsync();
