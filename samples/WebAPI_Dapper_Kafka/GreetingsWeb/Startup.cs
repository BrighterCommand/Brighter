using System;
using System.Data.Common;
using Confluent.SchemaRegistry;
using DapperExtensions;
using DapperExtensions.Sql;
using FluentMigrator.Runner;
using Greetings_MySqlMigrations.Migrations;
using Greetings_SqliteMigrations.Migrations;
using GreetingsPorts.EntityMappers;
using GreetingsPorts.Handlers;
using GreetingsPorts.Policies;
using GreetingsWeb.Database;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Sqlite;
using Paramore.Darker.AspNetCore;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsWeb
{
    public class Startup
    {
        private const string _outBoxTableName = "Outbox";
        private IWebHostEnvironment _env;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _env = env;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseProblemDetails();

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

            ConfigureMigration(services);
            ConfigureDapper(services);
            ConfigureBrighter(services);
            ConfigureDarker(services);
        }

        private void ConfigureMigration(IServiceCollection services)
        {
            if (_env.IsDevelopment())
            {
                services
                    .AddFluentMigratorCore()
                    .ConfigureRunner(c =>
                    {
                        c.AddSQLite()
                            .WithGlobalConnectionString(DbConnectionString())
                            .ScanIn(typeof(SqlliteInitialCreate).Assembly).For.Migrations();
                    });
            }
            else
            {
                ConfigureProductionDatabase(GetDatabaseType(), services);
            }
        }

        private void ConfigureProductionDatabase(DatabaseType databaseType, IServiceCollection services)
        {
            switch (databaseType)
            {
                case DatabaseType.MySql:
                    ConfigureMySql(services);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported");
            }
        }

        private void ConfigureMySql(IServiceCollection services)
        {
            services
                .AddFluentMigratorCore()
                .ConfigureRunner(c => c.AddMySql5()
                    .WithGlobalConnectionString(DbConnectionString())
                    .ScanIn(typeof(MySqlInitialCreate).Assembly).For.Migrations()
                );
        }

        private void ConfigureDapper(IServiceCollection services)
        {
            ConfigureDapperByHost(GetDatabaseType(), services);

            DapperExtensions.DapperExtensions.SetMappingAssemblies(new[] { typeof(PersonMapper).Assembly });
            DapperAsyncExtensions.SetMappingAssemblies(new[] { typeof(PersonMapper).Assembly });
        }

        private static void ConfigureDapperByHost(DatabaseType databaseType, IServiceCollection services)
        {
            switch (databaseType)
            {
                case DatabaseType.Sqlite:
                    ConfigureDapperSqlite(services);
                    break;
                case DatabaseType.MySql:
                    ConfigureDapperMySql(services);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported");
            }
        }

        private static void ConfigureDapperSqlite(IServiceCollection services)
        {
            DapperExtensions.DapperExtensions.SqlDialect = new SqliteDialect();
            DapperAsyncExtensions.SqlDialect = new SqliteDialect();
        }

        private static void ConfigureDapperMySql(IServiceCollection services)
        {
            DapperExtensions.DapperExtensions.SqlDialect = new MySqlDialect();
            DapperAsyncExtensions.SqlDialect = new MySqlDialect();
         }

        private void ConfigureBrighter(IServiceCollection services)
        {
            var outboxConfiguration = new RelationalDatabaseConfiguration(
                DbConnectionString(),
                outBoxTableName: _outBoxTableName,
                //NOTE: With the Serdes serializer, if we don't use a binary payload, the payload will be corrupted
                binaryMessagePayload: true
            );
            services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

            var schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://localhost:8081" };
            var cachedSchemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);
            services.AddSingleton<ISchemaRegistryClient>(cachedSchemaRegistryClient);

            var kafkaConfiguration = new KafkaMessagingGatewayConfiguration
            {
                Name = "paramore.brighter.greetingsender", BootStrapServers = new[] { "localhost:9092" }
            };
            var producerRegistry = new KafkaProducerRegistryFactory(
                    kafkaConfiguration,
                    new KafkaPublication[]
                    {
                        new KafkaPublication
                        {
                            Topic = new RoutingKey("greeting.event"),
                            MessageSendMaxRetries = 3,
                            MessageTimeoutMs = 1000,
                            MaxInFlightRequestsPerConnection = 1,
                            MakeChannels = OnMissingChannel.Create
                        }
                    })
                .Create();

            (IAmAnOutbox outbox, Type transactionProvider) makeOutbox =
                OutboxExtensions.MakeOutbox(_env, GetDatabaseType(), outboxConfiguration);

            services.AddBrighter(options =>
                {
                    //we want to use scoped, so make sure everything understands that which needs to
                    options.HandlerLifetime = ServiceLifetime.Scoped;
                    options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                    options.MapperLifetime = ServiceLifetime.Singleton;
                    options.PolicyRegistry = new GreetingsPolicy();
                })
                .UseExternalBus<DbTransaction>((configure) =>
                {
                    configure.ProducerRegistry = producerRegistry;
                    configure.Outbox = makeOutbox.outbox;
                    configure.TransactionProvider = makeOutbox.transactionProvider;
                })
                .UseOutboxSweeper(options =>
                {
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

        private string DbConnectionString()
        {
            //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
            return _env.IsDevelopment() ? GetDevDbConnectionString() : GetConnectionString(GetDatabaseType());
        }

        private DatabaseType GetDatabaseType()
        {
            return Configuration[DatabaseGlobals.DATABASE_TYPE_ENV] switch
            {
                DatabaseGlobals.MYSQL => DatabaseType.MySql,
                DatabaseGlobals.MSSQL => DatabaseType.MsSql,
                DatabaseGlobals.POSTGRESSQL => DatabaseType.Postgres,
                DatabaseGlobals.SQLITE => DatabaseType.Sqlite,
                _ => throw new InvalidOperationException("Could not determine the database type")
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
                DatabaseType.MySql => Configuration.GetConnectionString("GreetingsMySql"),
                _ => throw new InvalidOperationException("Could not determine the database type")
            };
        }
    }
}
