using System.Text.Json;
using DbMaker;
using GreetingsApp.Messaging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.Observability;
using ConnectionResolver = Greetings_Sweeper.Database.ConnectionResolver;

JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

MessagingTransport messagingTransport =
    ConfigureTransport.TransportType(builder.Configuration[MessagingGlobals.BRIGHTER_TRANSPORT]);

RelationalDatabaseConfiguration outboxConfiguration = new(
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

builder.Services.AddBrighter(options =>
{
    options.InstrumentationOptions = InstrumentationOptions.All;
}).UseExternalBus(configure =>
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
