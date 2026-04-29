using System;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "ASB")]
public class AzureServiceBusMessagePublisherLocalHeaderTests
{
    [Fact]
    public void When_Converting_A_Message_The_ProducerTopic_Local_Header_Is_Stripped()
    {
        var header = new MessageHeader(
            messageId: Guid.NewGuid().ToString(),
            topic: new RoutingKey("reply.address"),
            messageType: MessageType.MT_COMMAND);
        header.Bag[Message.ProducerTopicHeaderName] = "the.real.producer.topic";
        header.Bag["customer.header"] = "should.survive";

        var message = new Message(header, new MessageBody("body"));

        var asbMessage = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message);

        // local header is stripped from the wire form...
        Assert.False(asbMessage.ApplicationProperties.ContainsKey(Message.ProducerTopicHeaderName));
        // ...but the original message keeps it (so InMemoryOutbox-by-reference retries still work)
        Assert.True(message.Header.Bag.ContainsKey(Message.ProducerTopicHeaderName));
        // unrelated bag entries still travel on the wire
        Assert.True(asbMessage.ApplicationProperties.ContainsKey("customer.header"));
    }
}
