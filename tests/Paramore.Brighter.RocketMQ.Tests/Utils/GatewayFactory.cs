using Org.Apache.Rocketmq;
using Paramore.Brighter.MessagingGateway.RocketMQ;

namespace Paramore.Brighter.RocketMQ.Tests.Utils;

public static class GatewayFactory
{
    public static RocketMessagingGatewayConnection CreateConnection()
    {
        return new RocketMessagingGatewayConnection(new ClientConfig.Builder()
            .SetEndpoints("localhost:8081")
            .EnableSsl(false)
            .SetRequestTimeout(TimeSpan.FromSeconds(10))
            .Build());
    }


    public static async Task<SimpleConsumer> CreateSimpleConsumer(RocketMessagingGatewayConnection connection, Publication publication)
        => await CreateSimpleConsumer(connection, publication.Topic!);

    public static async Task<SimpleConsumer> CreateSimpleConsumer(RocketMessagingGatewayConnection connection, string topic)
    {
        return await new SimpleConsumer.Builder()
            .SetClientConfig(connection.ClientConfig)
            .SetConsumerGroup(Guid.NewGuid().ToString())
            .SetAwaitDuration(TimeSpan.Zero)
            .SetSubscriptionExpression(new Dictionary<string, FilterExpression> { [topic] = new("*") })
            .Build();
    }

    public static async Task<Producer> CreateProducer(RocketMessagingGatewayConnection connection, Publication publication)
        => await CreateProducer(connection, publication.Topic!);

    public static async Task<Producer> CreateProducer(RocketMessagingGatewayConnection connection, string topic)
    {
        return await new Producer.Builder()
            .SetClientConfig(connection.ClientConfig)
            .SetMaxAttempts(connection.MaxAttempts)
            .SetTopics(topic)
            .Build();
    }
}
