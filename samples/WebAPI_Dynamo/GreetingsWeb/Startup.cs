using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using DbMaker;
using GreetingsEntities;
using GreetingsApp.Handlers;
using GreetingsApp.Messaging;
using GreetingsApp.Policies;
using GreetingsApp.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.Outbox.DynamoDB;
using Paramore.Darker.AspNetCore;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsWeb
{
    public class Startup
    {
        private const string _outBoxTableName = "Outbox";
        private IWebHostEnvironment _env;
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
            OutboxFactory.CreateOutbox(_client, services);
        }

        private void ConfigureBrighter(IServiceCollection services)
        {
            var producerRegistry = ConfigureTransport.MakeProducerRegistry(MessagingTransport.Rmq); 
            
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
                 configure.Outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration());
                 configure.ConnectionProvider = typeof(DynamoDbUnitOfWork);
                 configure.TransactionProvider = typeof(DynamoDbUnitOfWork);
                 configure.MaxOutStandingMessages = 5;
                 configure.MaxOutStandingCheckIntervalMilliSeconds = 500;
                 configure.OutBoxBag = new Dictionary<string, object> { { "Topic", "GreetingMade" } };
             })
             .UseOutboxSweeper(options => { options.Args.Add("Topic", "GreetingMade"); })
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

    }
}
