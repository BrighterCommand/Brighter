using System;
using System.Collections.Generic;
using System.Text;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

/// <summary>
/// An AMQP-null application property arrives as a null *value* rather than an absent key —
/// <c>BrokeredMessageWrapper</c> hands the SDK dictionary straight through. Reading it must not
/// throw: like a <see cref="UriFormatException"/>, a <see cref="NullReferenceException"/> here would
/// escape inside the consumer's receive loop, leaving the message undeliverable with no boundary at
/// which the failure could be reported.
/// </summary>
[Trait("Category", "ASB")]
public class AzureServiceBusNullDataSchemaPropertyTests
{
    private readonly AzureServiceBusMessageCreator _creator;

    public AzureServiceBusNullDataSchemaPropertyTests()
    {
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("test-sub"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test-topic"),
            messagePumpType: MessagePumpType.Reactor);

        _creator = new AzureServiceBusMessageCreator(subscription);
    }

    [Fact]
    public void When_receiving_a_message_with_a_null_dataschema_property_should_leave_it_null()
    {
        // Arrange — the key is present but its value is null, which is what an AMQP null looks like
        // The CloudEvents keys are written as string literals, not via ASBConstants, deliberately:
        // these are wire-format attribute names, so a change to a constant's *value* must fail a test
        // rather than silently rename an on-wire attribute. (ASBConstants is internal in any case.)
        var received = new BrokeredMessage
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("{\"key\":\"value\"}"),
            ApplicationProperties = new Dictionary<string, object>
            {
                { "MessageType", "MT_COMMAND" },
                { "cloudEvents:schema", null! }
            },
            LockToken = Guid.NewGuid().ToString(),
            SequenceNumber = 1L,
            Id = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };

        // Act
        var message = _creator.MapToBrighterMessage(received);

        // Assert
        Assert.Null(message.Header.DataSchema);
    }
}
