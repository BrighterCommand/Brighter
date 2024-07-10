using System;
using DbMaker;
using GreetingsApp.EntityGateway;
using GreetingsApp.Handlers;
using GreetingsApp.Messaging;
using GreetingsApp.Policies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Darker.AspNetCore;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsWeb
{
    public class Startup
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration; 

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
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

            ConfigureEfCore(services);
            ConfigureBrighter(services);
            ConfigureDarker(services);
        }

        private void ConfigureBrighter(IServiceCollection services)
        {
            MessagingTransport messagingTransport =
                ConfigureTransport.TransportType(_configuration[MessagingGlobals.BRIGHTER_TRANSPORT]);

            ConfigureTransport.AddSchemaRegistryMaybe(services, messagingTransport);
            
            var outboxConfiguration = new RelationalDatabaseConfiguration(
                ConnectionResolver.GreetingsDbConnectionString(_configuration),
                binaryMessagePayload: messagingTransport == MessagingTransport.Kafka
            );
            
            string dbType = _configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
            if (string.IsNullOrWhiteSpace(dbType))
                throw new InvalidOperationException("DbType is not set");
            
            (IAmAnOutbox outbox, Type transactionProvider, Type connectionProvider) = OutboxFactory.MakeOutbox(
                DbResolver.GetDatabaseType(dbType),
                outboxConfiguration,
                services);
            
            services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);
            
            IAmAProducerRegistry producerRegistry = ConfigureTransport.MakeProducerRegistry(messagingTransport);

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
                        configure.ProducerRegistry = producerRegistry;
                        configure.Outbox = outbox;
                        configure.TransactionProvider = transactionProvider;
                        configure.ConnectionProvider = connectionProvider;
                        configure.MaxOutStandingMessages = 5;
                        configure.MaxOutStandingCheckIntervalMilliSeconds = 500;
                    }
                )
                .UseOutboxSweeper(options =>
                {
                    options.TimerInterval = 5;
                    options.MinimumMessageAge = 5000;
                })
                .UseOutboxSweeper()
                .AutoFromAssemblies();
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

        private void ConfigureEfCore(IServiceCollection services)
        {
            string connectionString = ConnectionResolver.GreetingsDbConnectionString(_configuration);
            string dbType = _configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
            
            switch ()

            if (_env.IsDevelopment())
            {
                ConfigureSqlite(services, connectionString);
            }
            else //TODO: Add other Db types here??
            {
                ConfigureMySql(services, connectionString);
            }
        }

        private static void ConfigureMySql(IServiceCollection services, string connectionString)
        {
            services.AddDbContextPool<GreetingsEntityGateway>(builder =>
            {
                builder
                    .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), optionsBuilder =>
                    {
                        optionsBuilder.MigrationsAssembly("Greetings_MySqlMigrations");
                    })
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging();
            });
        }

        private static void ConfigureSqlite(IServiceCollection services, string connectionString)
        {
            services.AddDbContext<GreetingsEntityGateway>(
                builder =>
                {
                    builder.UseSqlite(connectionString,
                            optionsBuilder =>
                            {
                                optionsBuilder.MigrationsAssembly("Greetings_SqliteMigrations");
                            })
                        .EnableDetailedErrors()
                        .EnableSensitiveDataLogging();
                });
        }
    }
}
