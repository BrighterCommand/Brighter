using System;
using System.Data;
using System.Data.Common;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Handlers;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MySql;
using Paramore.Brighter.MySql.EntityFrameworkCore;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Sqlite;
using Paramore.Brighter.Sqlite.EntityFrameworkCore;
using Paramore.Darker.AspNetCore;
using Polly;
using Polly.Registry;

namespace GreetingsAdapters
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

            ConfigureEFCore(services);
            ConfigureBrighter(services);
            ConfigureDarker(services);
            
            CheckDbIsUp();
            CreateOutbox();
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
                 services.AddBrighter(options =>
                     {
                         //we want to use scoped, so make sure everything understands that which needs to
                         options.HandlerLifetime = ServiceLifetime.Scoped;
                         options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                         options.MapperLifetime = ServiceLifetime.Singleton;
                         options.PolicyRegistry = policyRegistry;
                     })
                     .UseExternalBus(new RmqMessageProducer(
                             new RmqMessagingGatewayConnection
                             {
                                 AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                                 Exchange = new Exchange("paramore.brighter.exchange"),
                             },
                             new RmqPublication
                             {
                                 MaxOutStandingMessages = 5,
                                 MaxOutStandingCheckIntervalMilliSeconds = 500,
                                 WaitForConfirmsTimeOutInMilliseconds = 1000,
                                 MakeChannels = OnMissingChannel.Create
                             }
                         )
                     )
                     .UseSqliteOutbox(new SqliteConfiguration(DbConnectionString(), _outBoxTableName), typeof(SqliteConnectionProvider), ServiceLifetime.Singleton)
                     .UseSqliteTransactionConnectionProvider(typeof(SqliteEntityFrameworkConnectionProvider<GreetingsEntityGateway>), ServiceLifetime.Scoped)
                     .UseOutboxSweeper()
                     .AutoFromAssemblies();
            }
            else
            {
                services.AddBrighter(options =>
                    {
                        options.HandlerLifetime = ServiceLifetime.Scoped;
                        options.MapperLifetime = ServiceLifetime.Singleton;
                        options.PolicyRegistry = policyRegistry;
                    })
                    .UseExternalBus(new RmqMessageProducer(
                            new RmqMessagingGatewayConnection
                            {
                                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                                Exchange = new Exchange("paramore.brighter.exchange"),
                            },
                            new RmqPublication
                            {
                                MaxOutStandingMessages = 5,
                                MaxOutStandingCheckIntervalMilliSeconds = 500,
                                WaitForConfirmsTimeOutInMilliseconds = 1000,
                                MakeChannels = OnMissingChannel.Create
                            }
                        )
                    )
                    .UseMySqlOutbox(new MySqlConfiguration(DbConnectionString(), _outBoxTableName), typeof(MySqlConnectionProvider), ServiceLifetime.Singleton)
                    .UseMySqTransactionConnectionProvider(typeof(MySqlEntityFrameworkConnectionProvider<GreetingsEntityGateway>), ServiceLifetime.Scoped)
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

        private void ConfigureEFCore(IServiceCollection services)
        {
            string connectionString = DbConnectionString();

            if (_env.IsDevelopment())
            {
                services.AddDbContext<GreetingsEntityGateway>(
                    builder =>
                    {
                        builder.UseSqlite(connectionString, 
                            optionsBuilder =>
                            {
                                optionsBuilder.MigrationsAssembly("GreetingsPorts");
                            });
                    });
            }
            else
            {
               services.AddDbContext<GreetingsEntityGateway>(builder => 
               { builder.UseSqlServer(connectionString,
                   optionsBuilder =>
                   {
                       optionsBuilder.MigrationsAssembly("GreetingsPorts");
                   }); 
               });
            }
        }


        private void CreateOutbox()
        {
            try
            {
                var connectionString = DbConnectionString();

                if (_env.IsDevelopment())
                    CreateOutboxDevelopment(connectionString);
                else
                    CreateOutboxProduction(connectionString);
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"Issue with creating Outbox table, {e.Message}");
            }
        }

        private void CreateOutboxDevelopment(string connectionString)
        {
            using var sqlConnection = new SqliteConnection(connectionString);
            sqlConnection.Open();

            using var exists = sqlConnection.CreateCommand();
            exists.CommandText = SqliteOutboxBuilder.GetExists(_outBoxTableName);
            using var reader = exists.ExecuteReader(CommandBehavior.SingleRow);
            
            if (reader.HasRows) return;
            
            using var command = sqlConnection.CreateCommand();
            command.CommandText = SqliteOutboxBuilder.GetDDL(_outBoxTableName);
            command.ExecuteScalar();
        }

        private static void CreateOutboxProduction(string connectionString)
        {
            using var sqlConnection = new MySqlConnection(connectionString);
            sqlConnection.Open();
            
            using var command = sqlConnection.CreateCommand();
            command.CommandText = MySqlOutboxBuilder.GetDDL(_outBoxTableName);
            command.ExecuteScalar();
        }

        private string DbConnectionString()
        {
            //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
            return _env.IsDevelopment() ? "Filename=Greetings.db;Cache=Shared" : Configuration.GetConnectionString("Greetings");
        }
    }
}
