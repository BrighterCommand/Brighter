using System.Text.Json;
using DbMaker;
using GreetingsApp.Messaging;
using GreetingsApp.Requests;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using TransportMaker;

JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

var transport = builder.Configuration[MessagingGlobals.BRIGHTER_TRANSPORT];
if (string.IsNullOrEmpty(transport))
    throw new ArgumentNullException("No transport specified in configuration");

MessagingTransport messagingTransport =
    ConfigureTransport.TransportType(transport);

RelationalDatabaseConfiguration outboxConfiguration = new(
    ConnectionResolver.GreetingsDbConnectionString(builder.Configuration),
    binaryMessagePayload: messagingTransport == MessagingTransport.Kafka
);

builder.Services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

var dbType = builder.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
if (string.IsNullOrEmpty(dbType))
    throw new ArgumentNullException("No database type specified in configuration");

(IAmAnOutbox outbox, Type connectionProvider, Type transactionProvider) makeOutbox =
    OutboxFactory.MakeOutbox(
        DbResolver.GetDatabaseType(dbType),
        outboxConfiguration,
        builder.Services
    );

builder.Services.AddBrighter(options =>
{
    options.InstrumentationOptions = InstrumentationOptions.All;
}).UseExternalBus(configure =>
{
    configure.ProducerRegistry = ConfigureTransport.MakeProducerRegistry<GreetingMade>(messagingTransport);
    configure.Outbox = makeOutbox.outbox;
    configure.TransactionProvider = makeOutbox.transactionProvider;
    configure.ConnectionProvider = makeOutbox.connectionProvider;
    configure.MaxOutStandingMessages = 5;
    configure.MaxOutStandingCheckIntervalMilliSeconds = 500;
});

WebApplication app = builder.Build();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var content = new
        {
            Status = report.Status.ToString(),
            Results = report.Entries.ToDictionary(e => e.Key,
                e => new { Status = e.Value.Status.ToString(), e.Value.Description, e.Value.Duration }),
            report.TotalDuration
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(content, jsonOptions));
    }
});


app.Run();
