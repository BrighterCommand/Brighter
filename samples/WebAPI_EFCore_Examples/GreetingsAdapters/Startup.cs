using System;
using GreetingsAdapters.Services;
using GreetingsInteractors.EntityGateway;
using GreetingsInteractors.Handlers;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Darker.AspNetCore;
using Polly;
using Polly.Registry;

namespace GreetingsAdapters
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            ConfigureWebApi(app, env);
        }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
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

            string connectionString = ConfigureEFCore(services);


            ConfigureBrighter(services, connectionString);
            ConfigureDarker(services, connectionString);
        }

        private static void ConfigureBrighter(IServiceCollection services, string connectionString)
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
                .UseExternalOutbox(new MySqlOutbox(
                    new MySqlOutboxConfiguration(connectionString, "Outbox"))
                )
                .AutoFromAssemblies();
            
            services.AddHostedService<TimedOutboxSweeper>();
        }

        private void ConfigureDarker(IServiceCollection services, string connectionString)
        {
            services.AddDarker(options =>
                {
                    options.HandlerLifetime = ServiceLifetime.Scoped;
                    options.QueryProcessorLifetime = ServiceLifetime.Scoped;
                })
                .AddHandlersFromAssemblies(typeof(FindPersonByNameHandlerAsync).Assembly);
        }

        private string ConfigureEFCore(IServiceCollection services)
        {
            var connectionString = Configuration.GetConnectionString("Greetings");

            services.AddDbContext<GreetingsEntityGateway>(builder => { builder.UseSqlServer(connectionString); });
            return connectionString;
        }

 
        private static void ConfigureWebApi(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseProblemDetails();

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GreetingsAPI v1"));

            app.UseRouting();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}
