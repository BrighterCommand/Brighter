using System;
using DbMaker;
using GreetingsApp.Handlers;
using GreetingsApp.Policies;
using GreetingsApp.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Paramore.Darker.AspNetCore;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;
using TransportMaker;

namespace GreetingsWeb;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GreetingsAPI v1"));

        app.UseHttpsRedirection();
        app.UseRouting();

        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

        app.UseOpenTelemetryPrometheusScrapingEndpoint();
    }


    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvcCore().AddApiExplorer();
        services.AddControllers(options =>
            {
                options.RespectBrowserAcceptHeader = true;
            })
            .AddXmlSerializerFormatters();
        services.AddProblemDetails();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "GreetingsAPI", Version = "v1" });
        });

        ConfigureObservability(services);

        GreetingsDbFactory.ConfigureMigration(_configuration, services);
        ConfigureBrighter(services);
        ConfigureDarker(services);
    }

    private void ConfigureBrighter(IServiceCollection services)
    {
        var transport = _configuration[MessagingGlobals.BRIGHTER_TRANSPORT];
        if (string.IsNullOrWhiteSpace(transport))
            throw new InvalidOperationException("Transport is not set");
        
        MessagingTransport messagingTransport =
            ConfigureTransport.TransportType(transport);

        ConfigureTransport.AddSchemaRegistryMaybe(services, messagingTransport);

        RelationalDatabaseConfiguration outboxConfiguration = new(
            ConnectionResolver.DbConnectionString(_configuration, ApplicationType.Greetings),
            binaryMessagePayload: messagingTransport == MessagingTransport.Kafka
        );
        services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

        string dbType = _configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        if (string.IsNullOrWhiteSpace(dbType))
            throw new InvalidOperationException("DbType is not set");

        var rdbms = DbResolver.GetDatabaseType(dbType);
        (IAmAnOutbox outbox, Type connectionProvider, Type transactionProvider) makeOutbox =
            OutboxFactory.MakeDapperOutbox(rdbms, outboxConfiguration);

        services.AddBrighter(options =>
            {
                //we want to use scoped, so make sure everything understands that which needs to
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Singleton;
                options.PolicyRegistry = new GreetingsPolicy();
            })
            .AddProducers(configure =>
            {
                configure.ProducerRegistry = ConfigureTransport.MakeProducerRegistry<GreetingMade>(messagingTransport);
                configure.Outbox = makeOutbox.outbox;
                configure.TransactionProvider = makeOutbox.transactionProvider;
                configure.ConnectionProvider = makeOutbox.connectionProvider;
                configure.MaxOutStandingMessages = 5;
                configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
            })
            .AutoFromAssemblies([typeof(AddPersonHandlerAsync).Assembly]);
    }

    private void ConfigureDarker(IServiceCollection services)
    {
        services.AddDarker(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.QueryProcessorLifetime = ServiceLifetime.Scoped;
            })
            .AddHandlersFromAssemblies(typeof(FindPersonByNameHandlerAsync).Assembly)
            .AddJsonQueryLogging()
            .AddPolicies(new GreetingsPolicy());
    }

    private void ConfigureObservability(IServiceCollection services)
    {
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole();
            loggingBuilder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddConsoleExporter();
            });

            services.AddOpenTelemetry()
                .ConfigureResource(builder =>
                {
                    builder.AddService(
                        serviceName: "GreetingsWeb",
                        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                        serviceInstanceId: Environment.MachineName);
                })
                .WithTracing(builder =>
                {
                    builder
                        .AddBrighterInstrumentation()
                        .AddSource("RabbitMQ.Client.*")
                        .SetTailSampler<AlwaysOnSampler>()
                        .AddAspNetCoreInstrumentation()
                        .AddConsoleExporter()
                        .AddOtlpExporter(options =>
                        {
                            options.Protocol = OtlpExportProtocol.Grpc;
                        });
                })
                .WithMetrics(builder => builder
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter()
                    .AddPrometheusExporter()
                    .AddOtlpExporter()
                    .AddBrighterInstrumentation()
                );
        });
    }
}
