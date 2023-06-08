using System;
using DapperExtensions;
using DapperExtensions.Sql;
using FluentMigrator.Runner;
using Greetings_MySqlMigrations.Migrations;
using Greetings_PostgreSqlMigrations.Migrations;
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
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Darker.AspNetCore;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsWeb
{
    public class Startup
    {
        private const string OUTBOX_TABLE_NAME = "Outbox";
        
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
        }

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

            services.AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter())
                .WithMetrics(builder => builder
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter());

            ConfigureMigration(services);
            ConfigureDapper();
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
                    .ScanIn(typeof(MsSqlInitialCreate).Assembly).For.Migrations()
                );
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

        private void ConfigurePostgreSql(IServiceCollection services)
        {
            //TODO: add Postgres migrations
            services
                .AddFluentMigratorCore()
                .ConfigureRunner(c => c.AddMySql5()
                    .WithGlobalConnectionString(DbConnectionString())
                    .ScanIn(typeof(PostgreSqlInitialCreate).Assembly).For.Migrations()
                );
        }

        private void ConfigureSqlite(IServiceCollection services)
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
        
        private void ConfigureDapper()
        {
            ConfigureDapperByHost(GetDatabaseType());

            DapperExtensions.DapperExtensions.SetMappingAssemblies(new[] { typeof(PersonMapper).Assembly });
            DapperAsyncExtensions.SetMappingAssemblies(new[] { typeof(PersonMapper).Assembly });
        }

        private static void ConfigureDapperByHost(DatabaseType databaseType)
        {
            switch (databaseType)
            {
                case DatabaseType.Sqlite:
                    ConfigureDapperSqlite();
                    break;
                case DatabaseType.MySql:
                    ConfigureDapperMySql();
                    break;
                case DatabaseType.MsSql:
                    ConfigureDapperMsSql();
                    break;
                case DatabaseType.Postgres:
                    ConfigureDapperPostgreSql();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported");
            }
        }

        private static void ConfigureDapperMsSql()
        {
            DapperExtensions.DapperExtensions.SqlDialect = new SqlServerDialect();
            DapperAsyncExtensions.SqlDialect = new SqlServerDialect();
         }

        private static void ConfigureDapperSqlite()
        {
            DapperExtensions.DapperExtensions.SqlDialect = new SqliteDialect();
            DapperAsyncExtensions.SqlDialect = new SqliteDialect();
        }

        private static void ConfigureDapperMySql()
        {
            DapperExtensions.DapperExtensions.SqlDialect = new MySqlDialect();
            DapperAsyncExtensions.SqlDialect = new MySqlDialect();
        }

        private static void ConfigureDapperPostgreSql()
        {
            DapperExtensions.DapperExtensions.SqlDialect = new PostgreSqlDialect();
            DapperAsyncExtensions.SqlDialect = new PostgreSqlDialect();
         }

        private void ConfigureBrighter(IServiceCollection services)
        {
            var outboxConfiguration = new RelationalDatabaseConfiguration(
                DbConnectionString(),
                outBoxTableName:OUTBOX_TABLE_NAME
            );
            services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);

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
                        MaxOutStandingMessages = 5,
                        MaxOutStandingCheckIntervalMilliSeconds = 500,
                        WaitForConfirmsTimeOutInMilliseconds = 1000,
                        MakeChannels = OnMissingChannel.Create
                    }
                }
            ).Create();

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
                .UseExternalBus((configure) =>
                {
                    configure.ProducerRegistry = producerRegistry;
                    configure.Outbox = makeOutbox.outbox;
                    configure.TransactionProvider = makeOutbox.transactionProvider;
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
                DatabaseType.MySql => _configuration.GetConnectionString("GreetingsMySql"),
                DatabaseType.MsSql => _configuration.GetConnectionString("GreetingsMsSql"),
                DatabaseType.Postgres => _configuration.GetConnectionString("GreetingsPostgreSql"),
                DatabaseType.Sqlite => GetDevDbConnectionString(),
                _ => throw new InvalidOperationException("Could not determine the database type")
            };
        }
    }
}
