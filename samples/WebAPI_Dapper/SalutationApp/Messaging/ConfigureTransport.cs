using System;
using Confluent.Kafka;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.MessagingGateway.RMQ;
using SalutationApp.Requests;

namespace SalutationApp.Messaging;

public class ConfigureTransport
{
    public static IAmAProducerRegistry MakeProducerRegistry(MessagingTransport messagingTransport)
    {
        return messagingTransport switch
        {
            MessagingTransport.Rmq => GetRmqProducerRegistry(),
            MessagingTransport.Kafka => GetKafkaProducerRegistry(),
            _ => throw new ArgumentOutOfRangeException(nameof(messagingTransport),
                "Messaging transport is not supported")
        };
    }

    public static MessagingTransport GetTransportType(string brighterTransport)
    {
        return brighterTransport switch
        {
            MessagingGlobals.RMQ => MessagingTransport.Rmq,
            MessagingGlobals.KAFKA => MessagingTransport.Kafka,
            _ => throw new ArgumentOutOfRangeException(nameof(MessagingGlobals.BRIGHTER_TRANSPORT),
                "Messaging transport is not supported")
        };
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

    public static Subscription[] GetSubscriptions(MessagingTransport messagingTransport)
    {
        return messagingTransport switch
        {
            MessagingTransport.Rmq => GetRmqSubscriptions(),
            MessagingTransport.Kafka => GetKafkaSubscriptions(),
            _ => throw new ArgumentOutOfRangeException(nameof(messagingTransport),
                "Messaging transport is not supported")
        };
    }

    public static bool HasBinaryMessagePayload()
    {
        string? transport = Environment.GetEnvironmentVariable("BRIGHTER_TRANSPORT");
        if (string.IsNullOrWhiteSpace(transport))
            throw new InvalidOperationException("Transport is not set");

        return GetTransportType(transport) == MessagingTransport.Kafka;
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

    static IAmAProducerRegistry GetKafkaProducerRegistry()
    {
        IAmAProducerRegistry producerRegistry = new KafkaProducerRegistryFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "paramore.brighter.greetingsender", BootStrapServers = new[] { "localhost:9092" }
                },
                new KafkaPublication[]
                {
                    new()
                    {
                        Topic = new RoutingKey("SalutationReceived"),
                        RequestType = typeof(SalutationReceived),
                        MessageSendMaxRetries = 3,
                        MessageTimeoutMs = 1000,
                        MaxInFlightRequestsPerConnection = 1,
                        MakeChannels = OnMissingChannel.Create
                    }
                })
            .Create();

        return producerRegistry;
    }

    static IAmAChannelFactory GetRmqChannelFactory()
    {
        return new Paramore.Brighter.MessagingGateway.RMQ.ChannelFactory(
            new RmqMessageConsumerFactory(new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                Exchange = new Exchange("paramore.brighter.exchange")
            })
        );
    }

    static IAmAProducerRegistry GetRmqProducerRegistry()
    {
        IAmAProducerRegistry producerRegistry = new RmqProducerRegistryFactory(
            new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                Exchange = new Exchange("paramore.brighter.exchange")
            },
            new RmqPublication[]
            {
                new()
                {
                    Topic = new RoutingKey("SalutationReceived"),
                    RequestType = typeof(SalutationReceived),
                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                    MakeChannels = OnMissingChannel.Create
                }
            }
        ).Create();
        return producerRegistry;
    }

    static Subscription[] GetRmqSubscriptions()
    {
        Subscription[] subscriptions =
        {
            new RmqSubscription<GreetingMade>(
                new SubscriptionName("paramore.sample.salutationanalytics"),
                new ChannelName("SalutationAnalytics"),
                new RoutingKey("GreetingMade"),
                runAsync: true,
                timeoutInMilliseconds: 200,
                isDurable: true,
                makeChannels: OnMissingChannel
                    .Create) //change to OnMissingChannel.Validate if you have infrastructure declared elsewhere
        };
        return subscriptions;
    }

    static Subscription[] GetKafkaSubscriptions()
    {
        KafkaSubscription[] subscriptions =
        {
            new KafkaSubscription<GreetingMade>(
                new SubscriptionName("paramore.sample.salutationanalytics"),
                new ChannelName("SalutationAnalytics"),
                new RoutingKey("GreetingMade"),
                "kafka-GreetingsReceiverConsole-Sample",
                timeoutInMilliseconds: 100,
                offsetDefault: AutoOffsetReset.Earliest,
                commitBatchSize: 5,
                sweepUncommittedOffsetsIntervalMs: 10000,
                runAsync: true,
                makeChannels: OnMissingChannel.Create)
        };
        return subscriptions;
    }
}
