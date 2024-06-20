using System;
using GreetingsPorts.Database;
using GreetingsPorts.Handlers;
using GreetingsPorts.Messaging;
using GreetingsPorts.Policies;
using GreetingsWeb.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Darker.AspNetCore;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsWeb
{
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
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GreetingsAPI v1"));
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
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

            services.AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter())
                .WithMetrics(builder => builder
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter());

            GreetingsDbFactory.ConfigureMigration(_configuration, services);
            ConfigureBrighter(services);
            ConfigureDarker(services);
        }
 
        private void ConfigureBrighter(IServiceCollection services)
        {
            var messagingTransport = ConfigureTransport.TransportType(_configuration[MessagingGlobals.BRIGHTER_TRANSPORT]);
            
            ConfigureTransport.AddSchemaRegistryMaybe(services, messagingTransport);

            var outboxConfiguration = new RelationalDatabaseConfiguration(
                ConnectionResolver.DbConnectionString(_configuration),
                binaryMessagePayload: messagingTransport == MessagingTransport.Kafka
            );
            services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

            (IAmAnOutbox outbox, Type connectionProvider, Type transactionProvider) makeOutbox =
                OutboxFactory.MakeOutbox(
                    DbResolver.GetDatabaseType(_configuration[DatabaseGlobals.DATABASE_TYPE_ENV]), 
                    outboxConfiguration, 
                    services
                );

            services.AddBrighter(options =>
                {
                    //we want to use scoped, so make sure everything understands that which needs to
                    options.HandlerLifetime = ServiceLifetime.Scoped;
                    options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                    options.MapperLifetime = ServiceLifetime.Singleton;
                    options.PolicyRegistry = new GreetingsPolicy();
                })
                .UseExternalBus((configure) =>
                {
                    configure.ProducerRegistry = ConfigureTransport.MakeProducerRegistry(messagingTransport);
                    configure.Outbox = makeOutbox.outbox;
                    configure.TransactionProvider = makeOutbox.transactionProvider;
                    configure.ConnectionProvider = makeOutbox.connectionProvider;
                    configure.MaxOutStandingMessages = 5;
                    configure.MaxOutStandingCheckIntervalMilliSeconds = 500;
                })
                .AutoFromAssemblies(typeof(AddPersonHandlerAsync).Assembly);
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

 
    }
}
