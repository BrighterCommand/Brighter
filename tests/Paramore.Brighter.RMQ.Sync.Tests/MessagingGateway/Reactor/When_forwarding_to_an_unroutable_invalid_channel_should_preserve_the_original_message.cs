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


namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Property("Category", "RMQ")]
[System.Obsolete]
public class RmqInvalidMessageForwardingFailureTests
{
    [Test]
    public async System.Threading.Tasks.Task When_forwarding_to_an_unroutable_invalid_channel_should_preserve_the_original_message()
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
            deadLetterChannelName: new ChannelName(deadLetterRoutingKey.Value),
            deadLetterRoutingKey: deadLetterRoutingKey);
        subscription.InvalidMessageRoutingKey = invalidRoutingKey;

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
                queueName: subscription.DeadLetterChannelName!,
                routingKey: deadLetterRoutingKey,
                isDurable: false,
                makeChannels: OnMissingChannel.Assume);
            var message = new Message(
                new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
                new MessageBody("unacceptable message"));
            var reason = new MessageRejectionReason(RejectionReason.Unacceptable, "Invalid payload");

            consumer.Receive(TimeSpan.Zero);
            producer.Send(message);
            var received = consumer.Receive(TimeSpan.FromSeconds(10)).Single();
            await Assert.That(received.Id).IsEqualTo(message.Id);

            administration.QueueUnbind(invalidRoutingKey.Value, connection.Exchange.Name, invalidRoutingKey.Value, null);

            //Act
            Exception? exception = null;
            try
            {
                consumer.Reject(received, reason);
            }
            catch (Exception e)
            {
                exception = e;
            }

            //Assert
            await Assert.That(exception).IsTypeOf<ChannelFailureException>();
            await Assert.That(received.Header.Topic).IsEqualTo(routingKey);
            consumer.Nack(received);
            var redelivered = consumer.Receive(TimeSpan.FromSeconds(10)).Single();
            await Assert.That(redelivered.Id).IsEqualTo(message.Id);
            consumer.Acknowledge(redelivered);
            await Assert.That(invalidConsumer.Receive(TimeSpan.FromMilliseconds(500)).Single().Header.MessageType).IsEqualTo(MessageType.MT_NONE);
            await Assert.That(deadLetterConsumer.Receive(TimeSpan.FromMilliseconds(500)).Single().Header.MessageType).IsEqualTo(MessageType.MT_NONE);
        }
        finally
        {
            administration.QueueDelete(subscription.ChannelName.Value, false, false);
            administration.QueueDelete(invalidRoutingKey.Value, false, false);
            administration.QueueDelete(deadLetterRoutingKey.Value, false, false);
        }
    }
}
