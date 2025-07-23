﻿using Paramore.Brighter.MessagingGateway.Pulsar;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Pulsar.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.Pulsar.Tests.MessagingGateway.Reactor;

[Trait("Category", "Pulsar")]
public class BufferedConsumerCloudEventsTests : IDisposable 
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly IAmAMessageProducerSync _producer;
    private const int BatchSize = 3;

    public BufferedConsumerCloudEventsTests()
    {
        var connection = GatewayFactory.CreateConnection(); 
        var publication = new PulsarPublication { Topic = Guid.NewGuid().ToString() };
        var consumer = GatewayFactory.CreateConsumer(connection, publication);
        var producer = GatewayFactory.CreateProducer(connection, publication);

        _consumer = new PulsarMessageConsumer(consumer);
        _producer = new PulsarMessageProducer(producer, publication, TimeProvider.System, InstrumentationOptions.None);
    }

    [Fact]
    public void When_uses_cloud_events()
    {
        _consumer.Purge();
        
        //Post one more than batch size messages
        var messageOne = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND)
            {
                Type = $"Type{Guid.NewGuid():N}",
                Subject = $"Subject{Guid.NewGuid():N}",
                Source = new Uri($"/component/{Guid.NewGuid()}", UriKind.RelativeOrAbsolute),
                DataSchema = new Uri("https://example.com/storage/tenant/container", UriKind.RelativeOrAbsolute)
            }, new MessageBody("test content One"));
        
        _producer.Send(messageOne);

        //let them arrive
        Thread.Sleep(5000);

        //Now retrieve messages from the consumer
        var messages = _consumer.Receive(TimeSpan.FromMilliseconds(1000));

        //We should only have three messages
        Assert.Single(messages);

        Assert.Equal(messageOne.Header.MessageId, messages[0].Header.MessageId);
        Assert.Equal(messageOne.Header.Subject, messages[0].Header.Subject);
        Assert.Equal(messageOne.Header.Type, messages[0].Header.Type);
        Assert.Equal(messageOne.Header.Source, messages[0].Header.Source);
        Assert.Equal(messageOne.Header.DataSchema, messages[0].Header.DataSchema);
    }

    public void Dispose()
    {
        _consumer.Purge();
    }
}
