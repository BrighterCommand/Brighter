using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using DbMaker;
using GreetingsEntities;
using GreetingsApp.Handlers;
using GreetingsApp.Policies;
using GreetingsApp.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.DynamoDB;
using Paramore.Darker.AspNetCore;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;
using TransportMaker;

namespace GreetingsWeb
{
    public class Startup
    {
        private readonly IWebHostEnvironment _env;
        private IAmazonDynamoDB _client;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _env = env;
        }

        public IConfiguration Configuration { get; }

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

            ConfigureDynamo(services);
            ConfigureBrighter(services);
            ConfigureDarker(services);
        }

        private void ConfigureDynamo(IServiceCollection services)
        {
            _client = ConnectionResolver.CreateAndRegisterClient(services, _env.IsDevelopment());
            DbFactory.CreateEntityStore<Person>(_client);
            OutboxFactory.MakeDynamoOutbox(_client);
        }

        private void ConfigureBrighter(IServiceCollection services)
        {
            var transport = Configuration[MessagingGlobals.BRIGHTER_TRANSPORT];
            if (string.IsNullOrWhiteSpace(transport))
                throw new InvalidOperationException("Transport is not set");
        
            MessagingTransport messagingTransport =
                ConfigureTransport.TransportType(transport);

            ConfigureTransport.AddSchemaRegistryMaybe(services, messagingTransport);
            
            var producerRegistry = ConfigureTransport.MakeProducerRegistry<GreetingMade>(messagingTransport); 
            
            services.AddBrighter(options =>
             {
                 //we want to use scoped, so make sure everything understands that which needs to
                 options.HandlerLifetime = ServiceLifetime.Scoped;
                 options.MapperLifetime = ServiceLifetime.Singleton;
                 options.PolicyRegistry = new GreetingsPolicy();
             })
             .AddProducers((configure) =>
             {
                 configure.ProducerRegistry = producerRegistry;
                 configure.Outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(), TimeProvider.System);
                 configure.ConnectionProvider = typeof(DynamoDbUnitOfWork);
                 configure.TransactionProvider = typeof(DynamoDbUnitOfWork);
                 configure.MaxOutStandingMessages = 5;
                 configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
                 configure.OutBoxBag = new Dictionary<string, object> { { "Topic", "GreetingMade" } };
             })
             .AutoFromAssemblies([typeof(AddPersonHandlerAsync).Assembly]);
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

    }
}
