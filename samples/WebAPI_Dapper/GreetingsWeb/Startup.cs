using System;
using DapperExtensions;
using DapperExtensions.Sql;
using FluentMigrator.Runner;
using Greetings_SqliteMigrations.Migrations;
using GreetingsPorts.Handlers;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using Paramore.Brighter;
using Paramore.Brighter.Dapper;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MySql;
using Paramore.Brighter.MySql.Dapper;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Sqlite;
using Paramore.Brighter.Sqlite.Dapper;
using Paramore.Darker.AspNetCore;
using Polly;
using Polly.Registry;

namespace Greetingsweb
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
                    .ConfigureRunner(c => c.AddSQLite()
                        .WithGlobalConnectionString(DbConnectionString())
                        .ScanIn(typeof(SqlliteInitialCreate).Assembly).For.Migrations()
                    );
            }
            else
            {
                services
                    .AddFluentMigratorCore()
                    .ConfigureRunner(c => c.AddMySql5()
                        .WithGlobalConnectionString(DbConnectionString())
                        .ScanIn(typeof(SqlliteInitialCreate).Assembly).For.Migrations()
                    ); 
            }
             
        }

        private void ConfigureDapper(IServiceCollection services)
        {
            if (_env.IsDevelopment())
            {
                DapperExtensions.DapperExtensions.SqlDialect = new SqliteDialect();
                DapperAsyncExtensions.SqlDialect = new SqliteDialect();
            }
            else
            {
                DapperExtensions.DapperExtensions.SqlDialect = new MySqlDialect();
                DapperAsyncExtensions.SqlDialect = new MySqlDialect();
            }
        }

        private void CheckDbIsUp()
        {
            string connectionString = DbConnectionString();
            
            var policy = Policy.Handle<MySqlException>().WaitAndRetryForever(
                retryAttempt => TimeSpan.FromSeconds(2),
                (exception, timespan) =>
                {
                    Console.WriteLine($"Healthcheck: Waiting for the database {connectionString} to come online - {exception.Message}");
                });

            policy.Execute(() =>
            {
                //don't check this for SQlite in development
                if (!_env.IsDevelopment())
                {
                    using (var conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();
                    }
                }
            });
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

            if (_env.IsDevelopment())
            {
                    services.AddSingleton(new DbConnectionStringProvider(DbConnectionString()));
                    services.AddScoped(typeof(IUnitOfWork), typeof(Paramore.Brighter.Sqlite.Dapper.UnitOfWork));
                    
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
                             new RmqPublication[]{
                                 new RmqPublication
                                {
                                    Topic = new RoutingKey("GreetingMade"),
                                    MaxOutStandingMessages = 5,
                                    MaxOutStandingCheckIntervalMilliSeconds = 500,
                                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                                    MakeChannels = OnMissingChannel.Create
                                }}
                         ).Create()
                     )
                     .UseSqliteOutbox(new SqliteConfiguration(DbConnectionString(), _outBoxTableName), typeof(SqliteConnectionProvider), ServiceLifetime.Singleton)
                     .UseSqliteTransactionConnectionProvider(typeof(SqliteDapperConnectionProvider), ServiceLifetime.Scoped)
                     .UseOutboxSweeper(options =>
                     {
                         options.TimerInterval = 5;
                         options.MinimumMessageAge = 5000;
                     })
                     .AutoFromAssemblies();
            }
            else
            {
                services.AddSingleton(new DbConnectionStringProvider(DbConnectionString()));
                services.AddScoped(typeof(IUnitOfWork), typeof(Paramore.Brighter.MySql.Dapper.UnitOfWork));
                
                services.AddBrighter(options =>
                    {
                        options.HandlerLifetime = ServiceLifetime.Scoped;
                        options.MapperLifetime = ServiceLifetime.Singleton;
                        options.PolicyRegistry = policyRegistry;
                    })
                    .UseExternalBus(new RmqProducerRegistryFactory(
                            new RmqMessagingGatewayConnection
                            {
                                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@rabbitmq:5672")),
                                Exchange = new Exchange("paramore.brighter.exchange"),
                            },
                            new RmqPublication[] {
                                new RmqPublication
                                {
                                    Topic = new RoutingKey("GreetingMade"),
                                    MaxOutStandingMessages = 5,
                                    MaxOutStandingCheckIntervalMilliSeconds = 500,
                                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                                    MakeChannels = OnMissingChannel.Create
                                }}
                        ).Create()
                    )
                    .UseMySqlOutbox(new MySqlConfiguration(DbConnectionString(), _outBoxTableName), typeof(MySqlConnectionProvider), ServiceLifetime.Singleton)
                    .UseMySqTransactionConnectionProvider(typeof(MySqlDapperConnectionProvider), ServiceLifetime.Scoped)
                    .UseOutboxSweeper()
                    .AutoFromAssemblies();
            }

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
            return _env.IsDevelopment() ? "Filename=Greetings.db;Cache=Shared" : Configuration.GetConnectionString("Greetings");
        }
    }
}
