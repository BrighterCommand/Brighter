using Confluent.SchemaRegistry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.MessagingGateway.RMQ;

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

    public static IAmAProducerRegistry MakeProducerRegistry<T>(MessagingTransport messagingTransport) where T: IRequest
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

    private static IAmAProducerRegistry GetAsbProducerRegistry<T>() where T: IRequest
    {
        IAmAProducerRegistry producerRegistry = new AzureServiceBusProducerRegistryFactory(
                new ServiceBusVisualStudioCredentialClientProvider(".servicebus.windows.net"),
                new AzureServiceBusPublication[]
                {
                    new() { Topic = new RoutingKey("GreetingMade"), RequestType = typeof(T) }
                }
            )
            .Create();

        return producerRegistry;
    }

    public static IAmAProducerRegistry GetKafkaProducerRegistry<T>() where T:IRequest
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
                        Topic = new RoutingKey("GreetingMade"),
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

    public static void AddSchemaRegistryMaybe(IServiceCollection services, MessagingTransport messagingTransport)
    {
        if (messagingTransport != MessagingTransport.Kafka) return;

        SchemaRegistryConfig schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://localhost:8081" };
        CachedSchemaRegistryClient cachedSchemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);
        services.AddSingleton<ISchemaRegistryClient>(cachedSchemaRegistryClient);
    }

    public static IAmAProducerRegistry GetRmqProducerRegistry<T>() where T:IRequest
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
                    Topic = new RoutingKey("GreetingMade"),
                    RequestType = typeof(T),
                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                    MakeChannels = OnMissingChannel.Create
                }
            }
        ).Create();
        return producerRegistry;
    }
}
