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

#nullable enable

using System;
using System.Linq;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqInvalidMessageRoutingTests
{
    public static TheoryData<RejectionReason?, bool, bool, string> RoutingCases
    {
        get
        {
            var reasons = new (RejectionReason? Reason, bool Invalid, bool DeadLetter, string Destination)[]
            {
                (RejectionReason.Unacceptable, true, true, "invalid"),
                (RejectionReason.Unacceptable, true, false, "invalid"),
                (RejectionReason.Unacceptable, false, true, "dead-letter"),
                (RejectionReason.Unacceptable, false, false, "none"),
                (RejectionReason.DeliveryError, true, true, "dead-letter"),
                (RejectionReason.None, true, true, "dead-letter"),
                (null, true, true, "dead-letter"),
                (RejectionReason.DeliveryError, true, false, "none")
            };
            var cases = new TheoryData<RejectionReason?, bool, bool, string>();
            foreach (var (reason, invalid, deadLetter, destination) in reasons)
                cases.Add(reason, invalid, deadLetter, destination);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(RoutingCases))]
    public void When_rejecting_a_message_should_use_the_configured_rejection_route(RejectionReason? rejectionReason, bool hasInvalidChannel, bool hasDeadLetterQueue, string expectedDestination)
    {
        //Arrange
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());
        var invalidRoutingKey = new RoutingKey($"{routingKey.Value}.invalid");
        var deadLetterRoutingKey = new RoutingKey($"{routingKey.Value}.dlq");
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange"),
            DeadLetterExchange = new Exchange("paramore.brighter.exchange.dlq")
        };
        var subscription = new RmqSubscription(
            subscriptionName: new SubscriptionName(Guid.NewGuid().ToString()),
            channelName: new ChannelName(Guid.NewGuid().ToString()),
            routingKey: routingKey,
            requestType: typeof(Command),
            isDurable: false,
            messagePumpType: MessagePumpType.Reactor,
            deadLetterChannelName: hasDeadLetterQueue ? new ChannelName(deadLetterRoutingKey.Value) : null,
            deadLetterRoutingKey: hasDeadLetterQueue ? deadLetterRoutingKey : null);
        subscription.InvalidMessageRoutingKey = hasInvalidChannel ? invalidRoutingKey : null;

        var connectionFactory = new RabbitMQ.Client.ConnectionFactory { Uri = connection.AmpqUri.Uri };
        using var brokerConnection = connectionFactory.CreateConnection();
        using var administration = brokerConnection.CreateModel();
        try
        {
            using var producer = new RmqMessageProducer(connection);
            var factory = new RmqMessageConsumerFactory(connection);
            using var consumer = factory.Create(subscription);
            using var invalidConsumer = new RmqMessageConsumer(
                connection: connection,
                queueName: new ChannelName(invalidRoutingKey.Value),
                routingKey: invalidRoutingKey,
                isDurable: false,
                makeChannels: OnMissingChannel.Assume);
            using var deadLetterConsumer = new RmqMessageConsumer(
                connection: connection,
                queueName: new ChannelName(deadLetterRoutingKey.Value),
                routingKey: deadLetterRoutingKey,
                isDurable: false,
                makeChannels: OnMissingChannel.Assume);
            var message = new Message(
                new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
                new MessageBody("unacceptable message"));
            var reason = rejectionReason.HasValue ? new MessageRejectionReason(rejectionReason.Value, "Rejected payload") : null;

            consumer.Receive(TimeSpan.Zero);
            producer.Send(message);
            var received = consumer.Receive(TimeSpan.FromSeconds(10)).Single();
            Assert.Equal(message.Id, received.Id);

            //Act
            var rejected = consumer.Reject(received, reason);

            //Assert
            Assert.True(rejected);
            Assert.Equal(routingKey, received.Header.Topic);
            var invalidMessage = hasInvalidChannel
                ? invalidConsumer.Receive(expectedDestination == "invalid" ? TimeSpan.FromSeconds(10) : TimeSpan.FromMilliseconds(500)).Single()
                : new Message();
            if (expectedDestination == "invalid")
            {
                Assert.Equal(message.Id, invalidMessage.Id);
                Assert.Equal(message.Body.Value, invalidMessage.Body.Value);
                Assert.Equal(routingKey.Value, invalidMessage.Header.Bag[HeaderNames.ORIGINAL_TOPIC].ToString());
                Assert.Equal(MessageType.MT_COMMAND.ToString(), invalidMessage.Header.Bag[HeaderNames.ORIGINAL_TYPE].ToString());
                Assert.Equal(RejectionReason.Unacceptable.ToString(), invalidMessage.Header.Bag[HeaderNames.REJECTION_REASON].ToString());
                Assert.Equal(reason!.Description, invalidMessage.Header.Bag[HeaderNames.REJECTION_MESSAGE].ToString());
                Assert.True(DateTimeOffset.TryParse(invalidMessage.Header.Bag[HeaderNames.REJECTION_TIMESTAMP].ToString(), out var rejectedAt));
                Assert.InRange(rejectedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
                invalidConsumer.Acknowledge(invalidMessage);
            }
            else
            {
                Assert.Equal(MessageType.MT_NONE, invalidMessage.Header.MessageType);
            }

            var deadLetterMessage = hasDeadLetterQueue
                ? deadLetterConsumer.Receive(expectedDestination == "dead-letter" ? TimeSpan.FromSeconds(10) : TimeSpan.FromMilliseconds(500)).Single()
                : new Message();
            if (expectedDestination == "dead-letter")
            {
                Assert.Equal(message.Id, deadLetterMessage.Id);
                Assert.Equal(message.Body.Value, deadLetterMessage.Body.Value);
                deadLetterConsumer.Acknowledge(deadLetterMessage);
            }
            else
            {
                Assert.Equal(MessageType.MT_NONE, deadLetterMessage.Header.MessageType);
            }
            consumer.Dispose();
            Assert.Null(administration.BasicGet(subscription.ChannelName.Value, true));
        }
        finally
        {
            administration.QueueDelete(subscription.ChannelName.Value, false, false);
            administration.QueueDelete(invalidRoutingKey.Value, false, false);
            administration.QueueDelete(deadLetterRoutingKey.Value, false, false);
        }
    }
}
