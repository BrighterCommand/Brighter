using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.DynamoDB;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.Outbox.DynamoDB;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using SalutationEntities;
using SalutationPorts.Policies;
using SalutationPorts.Requests;

namespace SalutationAnalytics
{
    static class Program
    {
        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureHostConfiguration(configurationBuilder =>
                {
                    configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
                    configurationBuilder.AddJsonFile("appsettings.json", optional: true);
                    configurationBuilder.AddJsonFile($"appsettings.{GetEnvironment()}.json", optional: true);
                    configurationBuilder.AddEnvironmentVariables(prefix: "ASPNETCORE_"); //NOTE: Although not web, we use this to grab the environment
                    configurationBuilder.AddEnvironmentVariables(prefix: "BRIGHTER_");
                    configurationBuilder.AddCommandLine(args);
                })
                .ConfigureLogging((context, builder) =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var credentials = CreateCredentials();
                    IAmazonDynamoDB client = CreateAndRegisterClient(credentials, hostContext, services);
                    ConfigureDynamo(client, hostContext, services);
                    ConfigureBrighter(credentials, client, hostContext, services);
                })
                .UseConsoleLifetime();

        private static void ConfigureBrighter(
            AWSCredentials awsCredentials,
            IAmazonDynamoDB dynamoDb,
            HostBuilderContext hostContext,
            IServiceCollection services)
        {
            var subscriptions = new Subscription[]
            {
                new RmqSubscription<GreetingMade>(
                    new SubscriptionName("paramore.sample.salutationanalytics"),
                    new ChannelName("SalutationAnalytics"),
                    new RoutingKey("GreetingMade"),
                    runAsync: true,
                    timeoutInMilliseconds: 200,
                    isDurable: true,
                    makeChannels: OnMissingChannel.Create), //change to OnMissingChannel.Validate if you have infrastructure declared elsewhere
            };

            var host = hostContext.HostingEnvironment.IsDevelopment() ? "localhost" : "rabbitmq";

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri($"amqp://guest:guest@{host}:5672")), Exchange = new Exchange("paramore.brighter.exchange")
            };

            var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnection);

            services.AddServiceActivator(options =>
                {
                    options.Subscriptions = subscriptions;
                    options.ChannelFactory = new ChannelFactory(rmqMessageConsumerFactory);
                    options.UseScoped = true;
                    options.HandlerLifetime = ServiceLifetime.Scoped;
                    options.MapperLifetime = ServiceLifetime.Singleton;
                    options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                    options.PolicyRegistry = new SalutationPolicy();
                })
                .ConfigureJsonSerialisation((options) =>
                {
                    //We don't strictly need this, but added as an example
                    options.PropertyNameCaseInsensitive = true;
                })
                .UseExternalBus(new RmqProducerRegistryFactory(
                        rmqConnection,
                        new RmqPublication[]
                        {
                            new RmqPublication
                            {
                                Topic = new RoutingKey("SalutationReceived"),
                                MaxOutStandingMessages = 5,
                                MaxOutStandingCheckIntervalMilliSeconds = 500,
                                WaitForConfirmsTimeOutInMilliseconds = 1000,
                                MakeChannels = OnMissingChannel.Create
                            }
                        }
                    ).Create()
                )
                .AutoFromAssemblies()
                .UseExternalInbox(
                    ConfigureInbox(dynamoDb),
                    new InboxConfiguration(
                        scope: InboxScope.Commands,
                        onceOnly: true,
                        actionOnExists: OnceOnlyAction.Throw
                    )
                )
                .UseExternalOutbox(ConfigureOutbox(awsCredentials, dynamoDb))
                .UseDynamoDbTransactionConnectionProvider(typeof(DynamoDbUnitOfWork), ServiceLifetime.Scoped);

            services.AddHostedService<ServiceActivatorHostedService>();
        }


        private static void ConfigureDynamo(IAmazonDynamoDB dynamoDb, HostBuilderContext hostBuilderContext, IServiceCollection services)
        {
            CreateEntityStore(dynamoDb);
            CreateOutbox(dynamoDb, services);
            CreateInbox(dynamoDb, services);
        }

        private static IAmazonDynamoDB CreateAndRegisterClient(AWSCredentials credentials, HostBuilderContext hostBuilderContext, IServiceCollection services)
        {
            if (hostBuilderContext.HostingEnvironment.IsDevelopment())
            {
                return CreateAndRegisterLocalClient(credentials, services);
            }

            return CreateAndRegisterRemoteClient(services);
        }

        private static AWSCredentials CreateCredentials()
        {
            return new BasicAWSCredentials("FakeAccessKey", "FakeSecretKey");
        }

        private static IAmazonDynamoDB CreateAndRegisterLocalClient(AWSCredentials credentials, IServiceCollection services)
        {

            var clientConfig = new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" };

            var dynamoDb = new AmazonDynamoDBClient(credentials, clientConfig);
            services.Add(new ServiceDescriptor(typeof(IAmazonDynamoDB), dynamoDb));

            var dynamoDbConfiguration = new DynamoDbConfiguration(credentials, RegionEndpoint.EUWest1);
            services.Add(new ServiceDescriptor(typeof(DynamoDbConfiguration), dynamoDbConfiguration));

            return dynamoDb;
        }

        private static IAmazonDynamoDB CreateAndRegisterRemoteClient(IServiceCollection services)
        {
            throw new NotImplementedException();
        }

        private static void CreateEntityStore(IAmazonDynamoDB client)
        {
            var tableRequestFactory = new DynamoDbTableFactory();
            var dbTableBuilder = new DynamoDbTableBuilder(client);

            CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableRequest<Salutation>(
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

        private static void CreateOutbox(IAmazonDynamoDB client, IServiceCollection services)
        {
            var tableRequestFactory = new DynamoDbTableFactory();
            var dbTableBuilder = new DynamoDbTableBuilder(client);

            var createTableRequest = new DynamoDbTableFactory().GenerateCreateTableRequest<MessageItem>(
                new DynamoDbCreateProvisionedThroughput(
                    new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    new Dictionary<string, ProvisionedThroughput>
                    {
                        { "Outstanding", new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 } },
                        { "Delivered", new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 } }
                    }
                ));
            var outboxTableName = createTableRequest.TableName;
            (bool exist, IEnumerable<string> tables) hasTables = dbTableBuilder.HasTables(new string[] { outboxTableName }).Result;
            if (!hasTables.exist)
            {
                var buildTable = dbTableBuilder.Build(createTableRequest).Result;
                dbTableBuilder.EnsureTablesReady(new[] { createTableRequest.TableName }, TableStatus.ACTIVE).Wait();
            }
        }

        private static void CreateInbox(IAmazonDynamoDB client, IServiceCollection services)
        {
            var tableRequestFactory = new DynamoDbTableFactory();
            var dbTableBuilder = new DynamoDbTableBuilder(client);

            var createTableRequest = new DynamoDbTableFactory().GenerateCreateTableRequest<CommandItem<GreetingMade>>(
                new DynamoDbCreateProvisionedThroughput(
                    new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                    new Dictionary<string, ProvisionedThroughput>()
                ));
            
            var tableName = createTableRequest.TableName;
            (bool exist, IEnumerable<string> tables) hasTables = dbTableBuilder.HasTables(new string[] {tableName}).Result;
            if (!hasTables.exist)
            {
                var buildTable = dbTableBuilder.Build(createTableRequest).Result;
                dbTableBuilder.EnsureTablesReady(new[] {createTableRequest.TableName}, TableStatus.ACTIVE).Wait();
            }
        }

        private static IAmAnInbox ConfigureInbox(IAmazonDynamoDB dynamoDb)
        {
            return new DynamoDbInbox(dynamoDb);
        }

        private static IAmAnOutbox<Message> ConfigureOutbox(AWSCredentials credentials, IAmazonDynamoDB dynamoDb)
        {
            return new DynamoDbOutbox(dynamoDb, new DynamoDbConfiguration(credentials, RegionEndpoint.EUWest1));
        }


        private static string GetEnvironment()
        {
            //NOTE: Hosting Context will always return Production outside of ASPNET_CORE at this point, so grab it directly
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        }
    }
}
