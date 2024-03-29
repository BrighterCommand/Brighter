using System;
using Confluent.SchemaRegistry;
using FluentMigrator.Runner;
using Greetings_MySqlMigrations.Migrations;
using GreetingsPorts.Handlers;
using GreetingsPorts.Policies;
using GreetingsPorts.Requests;
using GreetingsWeb.Database;
using GreetingsWeb.Messaging;
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
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Darker.AspNetCore;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsWeb
{
    public class Startup
    {
        
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
        }

        private void AddSchemaRegistryMaybe(IServiceCollection services, MessagingTransport messagingTransport)
        {
            if (messagingTransport != MessagingTransport.Kafka) return;
            
            var schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://localhost:8081" };
            var cachedSchemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);
            services.AddSingleton<ISchemaRegistryClient>(cachedSchemaRegistryClient);
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

            ConfigureMigration(services);
            ConfigureBrighter(services);
            ConfigureDarker(services);
        }

        private void ConfigureMigration(IServiceCollection services)
        {
            //dev is always Sqlite
            if (_env.IsDevelopment())
                ConfigureSqlite(services);
            else
                ConfigureProductionDatabase(GetDatabaseType(), services);
        }

        private void ConfigureProductionDatabase(DatabaseType databaseType, IServiceCollection services)
        {
            switch (databaseType)
            {
                case DatabaseType.MySql:
                    ConfigureMySql(services);
                    break;
                case DatabaseType.MsSql:
                    ConfigureMsSql(services);
                    break;
                case DatabaseType.Postgres:
                    ConfigurePostgreSql(services);
                    break;
                case DatabaseType.Sqlite:
                    ConfigureSqlite(services);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported");
            }
        }

       private void ConfigureMsSql(IServiceCollection services)
        {
            services
                .AddFluentMigratorCore()
                .ConfigureRunner(c => c.AddSqlServer()
                    .WithGlobalConnectionString(DbConnectionString())
                    .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
                )
                .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration(){DbType = DatabaseType.MsSql.ToString()});
        }

        private void ConfigureMySql(IServiceCollection services)
        {
            services
                .AddFluentMigratorCore()
                .ConfigureRunner(c => c.AddMySql5()
                    .WithGlobalConnectionString(DbConnectionString())
                    .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
                )
                .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration(){DbType = DatabaseType.MySql.ToString()});
        }

        private void ConfigurePostgreSql(IServiceCollection services)
        {
            services
                .AddFluentMigratorCore()
                .ConfigureRunner(c => c.AddPostgres()
                    .ConfigureGlobalProcessorOptions(opt => opt.ProviderSwitches = "Force Quote=false")
                    .WithGlobalConnectionString(DbConnectionString())
                    .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
                )
                .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration(){DbType = DatabaseType.Postgres.ToString()});
        }

        private void ConfigureSqlite(IServiceCollection services)
        {
            services
                .AddFluentMigratorCore()
                .ConfigureRunner(c => c.AddSQLite()
                        .WithGlobalConnectionString(DbConnectionString())
                        .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
                )
                .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration(){DbType = DatabaseType.Sqlite.ToString()});
            }

        private void ConfigureBrighter(IServiceCollection services)
        {
            var messagingTransport = GetTransportType();
            
            AddSchemaRegistryMaybe(services, messagingTransport);

            var outboxConfiguration = new RelationalDatabaseConfiguration(
                DbConnectionString(),
                binaryMessagePayload: messagingTransport == MessagingTransport.Kafka
            );
            services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

            (IAmAnOutbox outbox, Type connectionProvider, Type transactionProvider) makeOutbox =
                OutboxExtensions.MakeOutbox(_env, GetDatabaseType(), outboxConfiguration, services);

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
                    configure.ProducerRegistry = ConfigureProducerRegistry(messagingTransport);
                    configure.Outbox = makeOutbox.outbox;
                    configure.TransactionProvider = makeOutbox.transactionProvider;
                    configure.ConnectionProvider = makeOutbox.connectionProvider;
                    configure.MaxOutStandingMessages = 5;
                    configure.MaxOutStandingCheckIntervalMilliSeconds = 500;
                })
                .UseOutboxSweeper(options => {
                    options.TimerInterval = 5;
                    options.MinimumMessageAge = 5000;
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

        private static IAmAProducerRegistry ConfigureProducerRegistry(MessagingTransport messagingTransport)
        {
            return messagingTransport switch
            {
                MessagingTransport.Rmq => GetRmqProducerRegistry(),
                MessagingTransport.Kafka => GetKafkaProducerRegistry(),
                _ => throw new ArgumentOutOfRangeException(nameof(messagingTransport), "Messaging transport is not supported")
            };
        }

        private string DbConnectionString()
        {
            //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
            return _env.IsDevelopment() ? GetDevDbConnectionString() : GetConnectionString(GetDatabaseType());
        }

        private DatabaseType GetDatabaseType()
        {
            return _configuration[DatabaseGlobals.DATABASE_TYPE_ENV] switch
            {
                DatabaseGlobals.MYSQL => DatabaseType.MySql,
                DatabaseGlobals.MSSQL => DatabaseType.MsSql,
                DatabaseGlobals.POSTGRESSQL => DatabaseType.Postgres,
                DatabaseGlobals.SQLITE => DatabaseType.Sqlite,
                _ => throw new ArgumentOutOfRangeException(nameof(DatabaseGlobals.DATABASE_TYPE_ENV), "Database type is not supported")
            };
        }

        private static string GetDevDbConnectionString()
        {
            return "Filename=Greetings.db;Cache=Shared";
        }

        private string GetConnectionString(DatabaseType databaseType)
        {
            return databaseType switch
            {
                DatabaseType.MySql => _configuration.GetConnectionString("GreetingsMySql"),
                DatabaseType.MsSql => _configuration.GetConnectionString("GreetingsMsSql"),
                DatabaseType.Postgres => _configuration.GetConnectionString("GreetingsPostgreSql"),
                DatabaseType.Sqlite => GetDevDbConnectionString(),
                _ => throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported") 
            };
        }
        
        private static IAmAProducerRegistry GetKafkaProducerRegistry()
        {
            var producerRegistry = new KafkaProducerRegistryFactory(
                    new KafkaMessagingGatewayConfiguration
                    {
                        Name = "paramore.brighter.greetingsender", BootStrapServers = new[] { "localhost:9092" }
                    },
                    new KafkaPublication[]
                    {
                        new KafkaPublication
                        {
                            Topic = new RoutingKey("GreetingMade"),
                            RequestType = typeof(GreetingMade),
                            MessageSendMaxRetries = 3,
                            MessageTimeoutMs = 1000,
                            MaxInFlightRequestsPerConnection = 1,
                            MakeChannels = OnMissingChannel.Create
                        }
                    })
                .Create();
            
            return producerRegistry;
        }
        
        private static IAmAProducerRegistry GetRmqProducerRegistry()
        {
            var producerRegistry = new RmqProducerRegistryFactory(
                new RmqMessagingGatewayConnection
                {
                    AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                    Exchange = new Exchange("paramore.brighter.exchange"),
                },
                new RmqPublication[]
                {
                    new RmqPublication
                    {
                        Topic = new RoutingKey("GreetingMade"),
                        RequestType = typeof(GreetingMade),
                        WaitForConfirmsTimeOutInMilliseconds = 1000,
                        MakeChannels = OnMissingChannel.Create
                    }
                }
            ).Create();
            return producerRegistry;
        }
       
       private MessagingTransport GetTransportType()
       {
           return _configuration[MessagingGlobals.BRIGHTER_TRANSPORT] switch
           {
               MessagingGlobals.RMQ => MessagingTransport.Rmq,
               MessagingGlobals.KAFKA => MessagingTransport.Kafka,
               _ => throw new ArgumentOutOfRangeException(nameof(MessagingGlobals.BRIGHTER_TRANSPORT),
                   "Messaging transport is not supported")
            };
        }
    }
}
