using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.MessagingGateway.RMQ.Async;

namespace TransportMaker;

/// <summary>
///     NOTE: This class is intended to allow us to switch the sample between transports.
///     Normally you can just inline this code into your application startup, as you do not need to be able to switch
///     betweeen different transports, just use one.
///     It is possible to merge producer registries, if you need to support multiple transports.
///     With that in mind, something like this class can be helpful for two reasons:
///     - Collate all your transport configuration in one place
///     - Allow you to use the same code in app and sweeper easily as they use the same settings
/// </summary>
public static class ConfigureTransport
{
    public static MessagingTransport TransportType(string brighterTransport)
    {
        return brighterTransport switch
        {
            MessagingGlobals.RMQ => MessagingTransport.Rmq,
            MessagingGlobals.KAFKA => MessagingTransport.Kafka,
            MessagingGlobals.ASB => MessagingTransport.Asb,
            _ => throw new ArgumentOutOfRangeException(nameof(MessagingGlobals.BRIGHTER_TRANSPORT),
                "Messaging transport is not supported")
        };
    }
    
    public static IAmAProducerRegistry MakeProducerRegistry<T>(MessagingTransport messagingTransport) where T : class, IRequest
    {
        return messagingTransport switch
        {
            MessagingTransport.Rmq => GetRmqProducerRegistry<T>(),
            MessagingTransport.Kafka => GetKafkaProducerRegistry<T>(),
            MessagingTransport.Asb => GetAsbProducerRegistry<T>(),
            _ => throw new ArgumentOutOfRangeException(nameof(messagingTransport),
                "Messaging transport is not supported")
        };
    }
    
    public static void AddSchemaRegistryMaybe(IServiceCollection services, MessagingTransport messagingTransport)
    {
        if (messagingTransport != MessagingTransport.Kafka) return;

        SchemaRegistryConfig schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://localhost:8081" };
        CachedSchemaRegistryClient cachedSchemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);
        services.AddSingleton<ISchemaRegistryClient>(cachedSchemaRegistryClient);
    }

    public static bool HasBinaryMessagePayload()
    {
        string? transport = Environment.GetEnvironmentVariable("BRIGHTER_TRANSPORT");
        if (string.IsNullOrWhiteSpace(transport))
            throw new InvalidOperationException("Transport is not set");

        return TransportType(transport) == MessagingTransport.Kafka;
    }
    
    static IAmAProducerRegistry GetRmqProducerRegistry<T>() where T : class, IRequest
    {
        IAmAProducerRegistry producerRegistry = new RmqProducerRegistryFactory(
                new RmqMessagingGatewayConnection
                {
                    AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                    Exchange = new Exchange("paramore.brighter.exchange")
                },
                new[]
                {
                    new RmqPublication
                    {
                        Topic = new RoutingKey(typeof(T).Name),
                        RequestType = typeof(T),
                        WaitForConfirmsTimeOutInMilliseconds = 1000,
                        MakeChannels = OnMissingChannel.Create
                    }
                }
            )
            .Create();

        return producerRegistry;
    }
    
    public static IAmAProducerRegistry GetKafkaProducerRegistry<T>() where T: class, IRequest
    {
        IAmAProducerRegistry producerRegistry = new KafkaProducerRegistryFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "paramore.brighter.greetingsender", BootStrapServers = new[] { "localhost:9092" }
                },
                new[]
                {
                    new KafkaPublication
                    {
                        Topic = new RoutingKey(typeof(T).Name),
                        RequestType = typeof(T),
                        MessageSendMaxRetries = 3,
                        MessageTimeoutMs = 1000,
                        MaxInFlightRequestsPerConnection = 1,
                        MakeChannels = OnMissingChannel.Create
                    }
                })
            .Create();

        return producerRegistry;
    }
    
    private static IAmAProducerRegistry GetAsbProducerRegistry<T>() where T : class, IRequest
    {
        IAmAProducerRegistry producerRegistry = new AzureServiceBusProducerRegistryFactory(
                new ServiceBusVisualStudioCredentialClientProvider(".servicebus.windows.net"),
                new AzureServiceBusPublication[]
                {
                    new() { Topic = new RoutingKey(typeof(T).Name), RequestType = typeof(T) }
                }
            )
            .Create();

        return producerRegistry;
    }
    
    public static IAmAChannelFactory GetChannelFactory(MessagingTransport messagingTransport)
    {
        return messagingTransport switch
        {
            MessagingTransport.Rmq => GetRmqChannelFactory(),
            MessagingTransport.Kafka => GetKafkaChannelFactory(),
            _ => throw new ArgumentOutOfRangeException(nameof(messagingTransport),
                "Messaging transport is not supported")
        };
    }
    
    static IAmAChannelFactory GetRmqChannelFactory()
    {
        return new Paramore.Brighter.MessagingGateway.RMQ.Async.ChannelFactory(
            new RmqMessageConsumerFactory(new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                Exchange = new Exchange("paramore.brighter.exchange")
            })
        );
    }

    static IAmAChannelFactory GetKafkaChannelFactory()
    {
        return new Paramore.Brighter.MessagingGateway.Kafka.ChannelFactory(
            new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "paramore.brighter", BootStrapServers = new[] { "localhost:9092" }
                }
            )
        );
    }

    public static Subscription[] GetSubscriptions<T>(MessagingTransport messagingTransport) where T : class, IRequest
    {
        return messagingTransport switch
        {
            MessagingTransport.Rmq => GetRmqSubscriptions<T>(),
            MessagingTransport.Kafka => GetKafkaSubscriptions<T>(),
            _ => throw new ArgumentOutOfRangeException(nameof(messagingTransport),
                "Messaging transport is not supported")
        };
    }

    static Subscription[] GetRmqSubscriptions<T>() where T : class, IRequest
    {
        Subscription[] subscriptions =
        {
            new RmqSubscription<T>(
                new SubscriptionName(typeof(T).Name),
                new ChannelName(typeof(T).Name),
                new RoutingKey(typeof(T).Name),
                messagePumpType: MessagePumpType.Proactor,
                timeOut: TimeSpan.FromMilliseconds(200),
                isDurable: true,
                makeChannels: OnMissingChannel.Create)
        };
        return subscriptions;
    }

    static Subscription[] GetKafkaSubscriptions<T>() where T : class, IRequest
    {
        Subscription[] subscriptions =
        {
            new KafkaSubscription<T>(
                new SubscriptionName(typeof(T).Name),
                new ChannelName(typeof(T).Name),
                new RoutingKey(typeof(T).Name),
                groupId: "kafka-GreetingsReceiverConsole-Sample",
                timeOut: TimeSpan.FromMilliseconds(100),
                offsetDefault: AutoOffsetReset.Earliest,
                commitBatchSize: 5,
                sweepUncommittedOffsetsInterval: TimeSpan.FromMilliseconds(10000),
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: OnMissingChannel.Create)
        };
        return subscriptions;
    }
}

 

