using System;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "ASB")]
public class AzureServiceBusMessagePublisherSessionIdTests
{
    [Theory]
    [InlineData("SessionId")] // as written by application code
    [InlineData("sessionId")] // as it returns from a camelCasing serialization round-trip (e.g. via the Outbox)
    public void When_Converting_A_Message_With_A_SessionId_Bag_Key_Of_Any_Casing_The_SessionId_Is_Set(string sessionIdKey)
    {
        // Brighter's JSON serialization uses JsonNamingPolicy.CamelCase, so a bag key written as
        // "SessionId" comes back as "sessionId" once the message round-trips through serialization
        // (for example when stored in and read back from an Outbox). The publisher must resolve the
        // SessionId regardless of the key's casing — and the reserved key must not leak onto the wire.
        const string expectedSessionId = "order-42";
        var header = new MessageHeader(
            messageId: Guid.NewGuid().ToString(),
            topic: new RoutingKey("test.topic"),
            messageType: MessageType.MT_COMMAND);
        header.Bag[sessionIdKey] = expectedSessionId;

        var message = new Message(header, new MessageBody("body"));

        var asbMessage = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message);

        // the session id is set on the outgoing message...
        Assert.Equal(expectedSessionId, asbMessage.SessionId);
        // ...and the reserved header does not leak into ApplicationProperties
        Assert.False(asbMessage.ApplicationProperties.ContainsKey(sessionIdKey));
    }
}
