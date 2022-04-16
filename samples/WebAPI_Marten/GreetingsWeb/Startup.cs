using System;
using GreetingsEntities;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.EntityGateway.Interfaces;
using GreetingsPorts.Handlers;
using GreetingsWeb.Extensions;
using Hellang.Middleware.ProblemDetails;
using Marten;
using Marten.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Npgsql;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Darker.AspNetCore;
using Polly;
using Polly.Registry;

namespace GreetingsWeb
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
            app.UseProblemDetails();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GreetingsAPI v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IGreetingsEntityGateway, GreetingsEntityGateway>();

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

            ConfigureMarten(services);
            ConfigureDarker(services);
            ConfigureBrighter(services);
        }

        private void ConfigureMarten(IServiceCollection services)
        {
            var connectionString = Configuration.GetConnectionString("MartenDb");

            services.AddMarten(opts =>
            {

                opts.Connection(connectionString);
                opts.RetryPolicy(MartenRetryPolicy.Twice(exception => exception is NpgsqlException ne && ne.IsTransient));
                opts.Schema.For<Person>()
                .SoftDeleted()
                .UniqueIndex(UniqueIndexType.Computed, x => x.Name);
            })
            .OptimizeArtifactWorkflow()
            .UseLightweightSessions();
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

        private void ConfigureBrighter(IServiceCollection services)
        {
            var retryPolicy = Policy.Handle<Exception>()
                .WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var retryPolicyAsync = Policy.Handle<Exception>()
                .WaitAndRetryAsync(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
            var circuitBreakerPolicyAsync = Policy.Handle<Exception>().CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(500));

            var policyRegistry = new PolicyRegistry()
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicyAsync },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicyAsync }
            };

            services.AddBrighter(options =>
            {
                // we want to use scoped, so make sure everything understands that which needs to
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Singleton;
                options.PolicyRegistry = policyRegistry;
            })
            .AutoFromAssemblies();
        }
    }
}
