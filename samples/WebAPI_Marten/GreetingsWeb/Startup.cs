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
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Darker.AspNetCore;

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
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GreetingsWeb v1"));
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
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "GreetingsWeb", Version = "v1" });
            });

            ConfigureDarker(services);
            ConfigureBrighter(services);
            ConfigureMarten(services);
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
            services.AddBrighter(options =>
            {
                // we want to use scoped, so make sure everything understands that which needs to
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Singleton;
            })
            .AutoFromAssemblies();
        }
    }
}
