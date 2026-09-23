#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading.Tasks;
using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "ASB")]
public class AzureServiceBusSubjectRoundTripTests
{
    [Theory]
    [InlineData("order-placed", false)]
    [InlineData("order-placed", true)]
    [InlineData("შეკვეთა/注文", false)]
    [InlineData("შეკვეთა/注文", true)]
    [InlineData(" ", false)]
    [InlineData(" ", true)]
    public async Task When_round_tripping_a_message_should_preserve_its_native_subject(
        string subject, bool removeCloudEventsSubject)
    {
        // Arrange
        var header = new MessageHeader(Id.Random(), new RoutingKey("orders"), MessageType.MT_EVENT,
            subject: subject);
        var original = new Message(header, new MessageBody("{}"));
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("orders-subscription"),
            channelName: new ChannelName("orders-channel"),
            routingKey: new RoutingKey("orders"),
            makeChannels: OnMissingChannel.Assume);

        // Act
        var published = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(original);
        if (removeCloudEventsSubject)
            published.ApplicationProperties.Remove("cloudEvents:subject");
        var serialized = published.GetRawAmqpMessage().ToBytes();
        var native = ServiceBusReceivedMessage.FromAmqpMessage(
            AmqpAnnotatedMessage.FromBytes(serialized), new BinaryData(Guid.NewGuid().ToByteArray()));
        await using var client = new InMemoryServiceBusClient(native);
        var factory = new AzureServiceBusConsumerFactory(client);
        await using var consumer = factory.CreateAsync(subscription);
        var messages = await consumer.ReceiveAsync(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(subject, native.Subject);
        var received = Assert.Single(messages);
        Assert.Equal(subject, received.Header.Subject);
        Assert.Equal(original.Id, received.Id);
        Assert.Equal(original.Body.Value, received.Body.Value);
        Assert.Equal(!removeCloudEventsSubject, received.Header.Bag.ContainsKey("cloudEvents:subject"));
        if (!removeCloudEventsSubject)
            Assert.Equal(subject, received.Header.Bag["cloudEvents:subject"]);
    }
}
