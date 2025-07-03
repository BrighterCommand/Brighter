using Org.Apache.Rocketmq;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Proactor;

[Trait("Category", "RocketMQ")]
public class BufferedConsumerCloudEventsTestsAsync : IAsyncDisposable 
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly IAmAMessageConsumerAsync _consumer;
    private readonly IAmAMessageProducerAsync _producer;
    private const int BatchSize = 3;

    public BufferedConsumerCloudEventsTestsAsync()
    {
        var connection = new RocketMessagingGatewayConnection(new ClientConfig.Builder()
            .SetEndpoints("localhost:8081")
            .EnableSsl(false)
            .SetRequestTimeout(TimeSpan.FromSeconds(10))
            .Build());
        
        var publication = new RocketPublication { Topic = "bt_mc_cloudevents_async" };
        
        var consumer = new SimpleConsumer.Builder()
            .SetClientConfig(connection.ClientConfig)
            .SetConsumerGroup(Guid.NewGuid().ToString())
            .SetAwaitDuration(TimeSpan.FromMinutes(1))
            .SetSubscriptionExpression(new Dictionary<string, FilterExpression>
            {
                ["bt_mc_cloudevents_async"] = new("*")
            })
            .Build()
            .GetAwaiter().GetResult();

        var producer = new Producer.Builder()
            .SetClientConfig(connection.ClientConfig)
            .SetMaxAttempts(connection.MaxAttempts)
            .SetTopics("bt_mc_cloudevents_async")
            .Build()
            .GetAwaiter().GetResult();


        _consumer = new RocketMessageConsumer(consumer, BatchSize, TimeSpan.FromSeconds(30));
        _producer = new RocketMessageProducer(connection, producer, publication);
         
    }

    [Fact]
    public async Task When_uses_cloud_events_async()
    {
        
        //Post one more than batch size messages
        var messageOne = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND)
            {
                Type = $"Type{Guid.NewGuid():N}",
                Subject = $"Subject{Guid.NewGuid():N}",
                Source = new Uri($"/component/{Guid.NewGuid()}", UriKind.RelativeOrAbsolute),
                DataSchema = new Uri("https://example.com/storage/tenant/container", UriKind.RelativeOrAbsolute)
            }, new MessageBody("test content One"));
        
        await _producer.SendAsync(messageOne);

        //let them arrive
        await Task.Delay(5000);

        //Now retrieve messages from the consumer
        var messages = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

        //We should only have three messages
        Assert.Single(messages);

        Assert.Equal(messageOne.Header.MessageId, messages[0].Header.MessageId);
        Assert.Equal(messageOne.Header.Subject, messages[0].Header.Subject);
        Assert.Equal(messageOne.Header.Type, messages[0].Header.Type);
        Assert.Equal(messageOne.Header.Source, messages[0].Header.Source);
        Assert.Equal(messageOne.Header.DataSchema, messages[0].Header.DataSchema);
    }

    public async ValueTask DisposeAsync()
    {
        await _consumer.PurgeAsync();
    }
}
