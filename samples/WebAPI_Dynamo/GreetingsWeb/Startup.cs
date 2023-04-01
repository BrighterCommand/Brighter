using System;
using System.Collections.Generic;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using GreetingsEntities;
using GreetingsPorts.Handlers;
using GreetingsPorts.Policies;
using Hellang.Middleware.ProblemDetails;
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

            ConfigureDynamo(services);
            ConfigureBrighter(services);
            ConfigureDarker(services);
        }

        private void ConfigureDynamo(IServiceCollection services)
        {
            IAmazonDynamoDB client = CreateAndRegisterClient(services);
            CreateEntityStore(client);
            CreateOutbox(client, services);
        }

       private IAmazonDynamoDB CreateAndRegisterClient(IServiceCollection services)
        {
            if (_env.IsDevelopment())
            {
                return CreateAndRegisterLocalClient(services);
            }

            return CreateAndRegisterRemoteClient(services);
        }
        private IAmazonDynamoDB CreateAndRegisterLocalClient(IServiceCollection services)
        {
            var credentials = new BasicAWSCredentials("FakeAccessKey", "FakeSecretKey");

            var clientConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = "http://localhost:8000"

            };

            var dynamoDb = new AmazonDynamoDBClient(credentials, clientConfig);
            services.Add(new ServiceDescriptor(typeof(IAmazonDynamoDB), dynamoDb));

            var dynamoDbConfiguration = new DynamoDbConfiguration();
            services.Add(new ServiceDescriptor(typeof(DynamoDbConfiguration), dynamoDbConfiguration));

            return dynamoDb;
        }

        private IAmazonDynamoDB CreateAndRegisterRemoteClient(IServiceCollection services)
        {
            throw new NotImplementedException();
        }

         private void CreateEntityStore(IAmazonDynamoDB client)
        {
            var tableRequestFactory = new DynamoDbTableFactory();
            var dbTableBuilder = new DynamoDbTableBuilder(client);

            CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableRequest<Person>(
                new DynamoDbCreateProvisionedThroughput
                (
                    new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 }
                )
            );

            var entityTableName = tableRequest.TableName;
            (bool exist, IEnumerable<string> tables) hasTables = dbTableBuilder.HasTables(new string[] { entityTableName }).Result;
            if (!hasTables.exist)
            {
                var buildTable = dbTableBuilder.Build(tableRequest).Result;
                dbTableBuilder.EnsureTablesReady(new[] { tableRequest.TableName }, TableStatus.ACTIVE).Wait();
            }
        }

        private void CreateOutbox(IAmazonDynamoDB client, IServiceCollection services)
        {
            var tableRequestFactory = new DynamoDbTableFactory();
            var dbTableBuilder = new DynamoDbTableBuilder(client);

            var createTableRequest = new DynamoDbTableFactory().GenerateCreateTableRequest<MessageItem>(
                    new DynamoDbCreateProvisionedThroughput(
                    new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                    new Dictionary<string, ProvisionedThroughput>
                    {
                        {"Outstanding", new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10}},
                        {"Delivered", new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10}}
                    }
                ));
            var outboxTableName = createTableRequest.TableName;
            (bool exist, IEnumerable<string> tables) hasTables = dbTableBuilder.HasTables(new string[] {outboxTableName}).Result;
            if (!hasTables.exist)
            {
                var buildTable = dbTableBuilder.Build(createTableRequest).Result;
                dbTableBuilder.EnsureTablesReady(new[] {createTableRequest.TableName}, TableStatus.ACTIVE).Wait();
            }
        }

        private void ConfigureBrighter(IServiceCollection services)
        {
            services.AddBrighter(options =>
             {
                 //we want to use scoped, so make sure everything understands that which needs to
                 options.HandlerLifetime = ServiceLifetime.Scoped;
                 options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                 options.MapperLifetime = ServiceLifetime.Singleton;
                 options.PolicyRegistry = new GreetingsPolicy();
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
                            OutBoxBag = new Dictionary<string, object>(){{"Topic", "GreetingMade"}},
                            MakeChannels = OnMissingChannel.Create
                        }}
                 ).Create()
             )
             .UseDynamoDbOutbox(ServiceLifetime.Singleton)
             .UseDynamoDbTransactionConnectionProvider(typeof(DynamoDbUnitOfWork), ServiceLifetime.Scoped)
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
