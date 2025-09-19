using System.Text.Json;
using DbMaker;
using Salutation_Sweeper.Extensions;
using SalutationApp.Requests;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Observability;
using TransportMaker;

JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
var brighterTracer = new BrighterTracer(TimeProvider.System);
builder.Services.AddSingleton<IAmABrighterTracer>(brighterTracer);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddOpenTelemetry(otel =>
{
    otel.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Greetings Sweeper"))
        .AddConsoleExporter();
});
builder.Services.AddOpenTelemetry()
    .ConfigureResource(builder =>
    {
        builder.AddService(
            serviceName: "Salutation Sweeper",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            serviceInstanceId: Environment.MachineName);
    }).WithTracing(tracing =>
    {
        tracing
            .AddSource(brighterTracer.ActivitySource.Name)
            .AddSource("RabbitMQ.Client.*")
            .SetSampler(new AlwaysOnSampler())
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter()
            .AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.Grpc;
            });
    }).WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter()
        .AddOtlpExporter()
    );

var transport = builder.Configuration[MessagingGlobals.BRIGHTER_TRANSPORT];
if (string.IsNullOrEmpty(transport))
    throw new ArgumentNullException("No transport specified in configuration");

MessagingTransport messagingTransport =
    ConfigureTransport.TransportType(transport);

RelationalDatabaseConfiguration outboxConfiguration = new(
    ConnectionResolver.DbConnectionString(builder.Configuration, ApplicationType.Salutations),
    binaryMessagePayload: messagingTransport == MessagingTransport.Rmq
);

builder.Services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

var dbType = builder.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
if (string.IsNullOrEmpty(dbType))
    throw new ArgumentNullException("No database type specified in configuration");

var rdbms = DbResolver.GetDatabaseType(dbType);
(IAmAnOutbox outbox, Type connectionProvider, Type transactionProvider) makeOutbox =
    OutboxFactory.MakeDapperOutbox(rdbms, outboxConfiguration);

builder.Services.AddBrighter(options =>
{
    options.InstrumentationOptions = InstrumentationOptions.All;
}).AddProducers(configure =>
{
    configure.ProducerRegistry = ConfigureTransport.MakeProducerRegistry<SalutationReceived>(messagingTransport);
    configure.Outbox = makeOutbox.outbox;
    configure.TransactionProvider = makeOutbox.transactionProvider;
    configure.ConnectionProvider = makeOutbox.connectionProvider;
    configure.MaxOutStandingMessages = 5;
    configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
})
.UseOutboxSweeper(options =>
{
    options.TimerInterval = 3;
    options.MinimumMessageAge = TimeSpan.FromSeconds(1);
    options.BatchSize = 10;
    options.UseBulk = false;
});

builder.Services.AddHealthChecks().BrighterOutboxHealthCheck();

WebApplication app = builder.Build();

app.UseRouting();

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
