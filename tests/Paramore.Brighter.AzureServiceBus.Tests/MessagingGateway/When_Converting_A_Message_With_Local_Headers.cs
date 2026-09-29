using System;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Category("ASB")]
public class AzureServiceBusMessagePublisherLocalHeaderTests
{
    [Test]
    public async Task When_Converting_A_Message_The_SequenceNumber_Bag_Entry_Is_Not_Written_To_ApplicationProperties()
    {
        // SequenceNumber is a broker-assigned system property on every delivery.
        // It must not round-trip through ApplicationProperties — if it did, a requeued
        // message would arrive back with SequenceNumber in both its native property and
        // ApplicationProperties, causing a duplicate-key crash in MapToBrighterMessage.
        var header = new MessageHeader(
            messageId: Guid.NewGuid().ToString(),
            topic: new RoutingKey("test.topic"),
            messageType: MessageType.MT_COMMAND);
        header.Bag["SequenceNumber"] = 42L;

        var message = new Message(header, new MessageBody("body"));

        var asbMessage = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message);

        await Assert.That(asbMessage.ApplicationProperties.ContainsKey("SequenceNumber")).IsFalse();
    }

    [Test]
    public async Task When_Converting_A_Message_The_ProducerTopic_Local_Header_Is_Stripped()
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
        await Assert.That(asbMessage.ApplicationProperties.ContainsKey(Message.ProducerTopicHeaderName)).IsFalse();
        // ...but the original message keeps it (so InMemoryOutbox-by-reference retries still work)
        await Assert.That(message.Header.Bag.ContainsKey(Message.ProducerTopicHeaderName)).IsTrue();
        // unrelated bag entries still travel on the wire
        await Assert.That(asbMessage.ApplicationProperties.ContainsKey("customer.header")).IsTrue();
    }
}
