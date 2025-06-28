using System.Text.Json;
using DbMaker;
using GreetingsApp.Requests;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.DynamoDB;
using TransportMaker;

JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

var transport = builder.Configuration[MessagingGlobals.BRIGHTER_TRANSPORT];
if (string.IsNullOrEmpty(transport))
    throw new ArgumentNullException("No transport specified in configuration");

MessagingTransport messagingTransport =
    ConfigureTransport.TransportType(transport);

var client = ConnectionResolver.CreateAndRegisterClient(builder.Services, builder.Environment.IsDevelopment());
OutboxFactory.MakeDynamoOutbox(client);

builder.Services.AddBrighter(options =>
{
    options.InstrumentationOptions = InstrumentationOptions.All;
}).UseExternalBus(configure =>
{
    configure.ProducerRegistry = ConfigureTransport.MakeProducerRegistry<GreetingMade>(messagingTransport, builder.Configuration.GetConnectionString("messaging"));
    configure.Outbox = new DynamoDbOutbox(client, new DynamoDbConfiguration(), TimeProvider.System);;
    configure.ConnectionProvider = typeof(DynamoDbUnitOfWork);
    configure.TransactionProvider = typeof(DynamoDbUnitOfWork);
    configure.MaxOutStandingMessages = 5;
    configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
    configure.OutBoxBag = new Dictionary<string, object> { { "Topic", "GreetingMade" } };
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
