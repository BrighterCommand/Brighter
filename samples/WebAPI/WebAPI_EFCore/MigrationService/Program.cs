using GreetingsApp.EntityGateway;
using MigrationService;
using SalutationApp.EntityGateway;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<GreetingsMigrator>();
builder.Services.AddHostedService<SalutationsMigrator>();

builder.AddServiceDefaults();

builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(builder.Environment.ApplicationName));

builder.AddMySqlDbContext<GreetingsEntityGateway>("Greetings");
//builder.AddMySqlDbContext<SalutationsEntityGateway>("Salutations");

var app = builder.Build();

app.Run();

public class SalutationsMigrator(IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}


