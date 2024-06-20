using System.Text.Json;
using Greetings_Sweeper.Database;
using GreetingsPorts.Database;
using GreetingsPorts.Messaging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.Observability;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};

var builder = WebApplication.CreateBuilder(args);

var messagingTransport = ConfigureTransport.TransportType(builder.Configuration[MessagingGlobals.BRIGHTER_TRANSPORT]);

var outboxConfiguration = new RelationalDatabaseConfiguration(
    ConnectionResolver.DbConnectionString(builder.Configuration),
    binaryMessagePayload: messagingTransport == MessagingTransport.Kafka
);

builder.Services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

(IAmAnOutbox outbox, Type connectionProvider, Type transactionProvider) makeOutbox =
    OutboxFactory.MakeOutbox(
        DbResolver.GetDatabaseType(builder.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV]), 
        outboxConfiguration, 
        builder.Services
    );

builder.Services.AddBrighter((options =>
{
    options.InstrumentationOptions = InstrumentationOptions.All;
})).UseExternalBus((configure) =>
{
    configure.ProducerRegistry = ConfigureTransport.MakeProducerRegistry(messagingTransport);
    configure.Outbox = makeOutbox.outbox;
    configure.TransactionProvider = makeOutbox.transactionProvider;
    configure.ConnectionProvider = makeOutbox.connectionProvider;
    configure.MaxOutStandingMessages = 5;
    configure.MaxOutStandingCheckIntervalMilliSeconds = 500;
}).UseOutboxSweeper(options =>
{
    options.TimerInterval = 5;
    options.MinimumMessageAge = 5000;
});

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var content = new
        {
            Status = report.Status.ToString(),
            Results = report.Entries.ToDictionary(e => e.Key, e => new
            {
                Status = e.Value.Status.ToString(),
                e.Value.Description,
                e.Value.Duration
            }),
            report.TotalDuration
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(content, jsonOptions));
    }
});


app.Run();

