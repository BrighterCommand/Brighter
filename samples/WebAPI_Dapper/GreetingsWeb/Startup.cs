using System;
using DapperExtensions;
using DapperExtensions.Sql;
using FluentMigrator.Runner;
using Greetings_MySqlMigrations.Migrations;
using Greetings_SqliteMigrations.Migrations;
using GreetingsPorts.EntityMappers;
using GreetingsPorts.Handlers;
using GreetingsWeb.Database;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Paramore.Brighter;
using Paramore.Brighter.Dapper;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Darker.AspNetCore;
using Polly;
using Polly.Registry;

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
            services.AddSingleton<DbConnectionStringProvider>(new DbConnectionStringProvider(DbConnectionString()));

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
            services.AddScoped<IUnitOfWork, Paramore.Brighter.Sqlite.Dapper.UnitOfWork>();
        }

        private static void ConfigureDapperMySql(IServiceCollection services)
        {
            DapperExtensions.DapperExtensions.SqlDialect = new MySqlDialect();
            DapperAsyncExtensions.SqlDialect = new MySqlDialect();
            services.AddScoped<IUnitOfWork, Paramore.Brighter.MySql.Dapper.UnitOfWork>();
        }

        private void ConfigureBrighter(IServiceCollection services)
        {
            var retryPolicy = Policy.Handle<Exception>()
                .WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
            var retryPolicyAsync = Policy.Handle<Exception>()
                .WaitAndRetryAsync(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicyAsync = Policy.Handle<Exception>().CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(500));
            var policyRegistry = new PolicyRegistry()
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy },
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicyAsync },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicyAsync }
            };

            services.AddSingleton(new DbConnectionStringProvider(DbConnectionString()));

            services.AddBrighter(options =>
                {
                    //we want to use scoped, so make sure everything understands that which needs to
                    options.HandlerLifetime = ServiceLifetime.Scoped;
                    options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                    options.MapperLifetime = ServiceLifetime.Singleton;
                    options.PolicyRegistry = policyRegistry;
                })
                .UseExternalBus(new RmqProducerRegistryFactory(
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
                    ).Create()
                )
                //NOTE: The extension method AddOutbox is defined locally to the sample, to allow us to switch between outbox
                //types easily. You may just choose to call the methods directly if you do not need to support multiple
                //db types (which we just need to allow you to see how to configure your outbox type).
                //It's also an example of how you can extend the DSL here easily if you have this kind of variability
                .AddOutbox(_env, GetDatabaseType(), DbConnectionString(), _outBoxTableName)
                .AutoFromAssemblies(typeof(AddPersonHandlerAsync).Assembly);
        }

        private void ConfigureDarker(IServiceCollection services)
        {
            services.AddDarker(options =>
                {
                    options.HandlerLifetime = ServiceLifetime.Scoped;
                    options.QueryProcessorLifetime = ServiceLifetime.Scoped;
                })
                .AddHandlersFromAssemblies(typeof(FindPersonByNameHandlerAsync).Assembly);
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
