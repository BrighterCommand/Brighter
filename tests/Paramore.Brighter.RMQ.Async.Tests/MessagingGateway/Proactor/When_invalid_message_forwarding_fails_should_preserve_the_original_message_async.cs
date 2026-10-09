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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Exceptions;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqInvalidMessageForwardingFailureTests
{
    [Theory]
    [InlineData(QueueType.Classic, false, false)]
    [InlineData(QueueType.Classic, true, false)]
    [InlineData(QueueType.Quorum, false, false)]
    [InlineData(QueueType.Quorum, true, false)]
    [InlineData(QueueType.Classic, true, true)]
    [InlineData(QueueType.Quorum, true, true)]
    public async Task When_invalid_message_forwarding_fails_should_preserve_the_original_message_async(QueueType queueType, bool useAsync, bool cancel)
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
            isDurable: true,
            messagePumpType: useAsync ? MessagePumpType.Proactor : MessagePumpType.Reactor,
            deadLetterChannelName: new ChannelName(deadLetterRoutingKey.Value),
            deadLetterRoutingKey: deadLetterRoutingKey,
            queueType: queueType);
        subscription.InvalidMessageRoutingKey = invalidRoutingKey;

        var connectionFactory = new RabbitMQ.Client.ConnectionFactory { Uri = connection.AmpqUri.Uri };
        await using var brokerConnection = await connectionFactory.CreateConnectionAsync();
        await using var administration = await brokerConnection.CreateChannelAsync();
        try
        {
            await using var producer = new RmqMessageProducer(connection);
            var factory = new RmqMessageConsumerFactory(connection);
            await using var consumer = useAsync
                ? (RmqMessageConsumer)factory.CreateAsync(subscription)
                : (RmqMessageConsumer)factory.Create(subscription);
            await using var invalidConsumer = new RmqMessageConsumer(
                connection: connection,
                queueName: new ChannelName(invalidRoutingKey.Value),
                routingKey: invalidRoutingKey,
                isDurable: true,
                makeChannels: OnMissingChannel.Assume);
            await using var deadLetterConsumer = new RmqMessageConsumer(
                connection: connection,
                queueName: subscription.DeadLetterChannelName!,
                routingKey: deadLetterRoutingKey,
                isDurable: true,
                makeChannels: OnMissingChannel.Assume);
            var message = new Message(
                new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
                new MessageBody("unacceptable message"));
            var reason = new MessageRejectionReason(RejectionReason.Unacceptable, "Invalid payload");

            await consumer.ReceiveAsync(TimeSpan.Zero);
            await producer.SendAsync(message);
            var received = (useAsync
                ? await consumer.ReceiveAsync(TimeSpan.FromSeconds(10))
                : consumer.Receive(TimeSpan.FromSeconds(10))).Single();
            Assert.Equal(message.Id, received.Id);

            using var cancellation = new CancellationTokenSource();
            if (cancel)
                cancellation.Cancel();
            else
                await administration.QueueUnbindAsync(invalidRoutingKey.Value, connection.Exchange.Name, invalidRoutingKey.Value);

            //Act
            var exception = await Record.ExceptionAsync(async () =>
            {
                if (useAsync)
                    await consumer.RejectAsync(received, reason, cancellation.Token);
                else
                    consumer.Reject(received, reason);
            });

            //Assert
            if (cancel)
                Assert.IsAssignableFrom<OperationCanceledException>(exception);
            else
                Assert.IsAssignableFrom<PublishException>(exception);
            Assert.Equal(routingKey, received.Header.Topic);
            await consumer.NackAsync(received);
            var redelivered = (await consumer.ReceiveAsync(TimeSpan.FromSeconds(10))).Single();
            Assert.Equal(message.Id, redelivered.Id);
            await consumer.AcknowledgeAsync(redelivered);
            Assert.Equal(MessageType.MT_NONE, (await invalidConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(500))).Single().Header.MessageType);
            Assert.Equal(MessageType.MT_NONE, (await deadLetterConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(500))).Single().Header.MessageType);
        }
        finally
        {
            await administration.QueueDeleteAsync(subscription.ChannelName.Value, false, false);
            await administration.QueueDeleteAsync(invalidRoutingKey.Value, false, false);
            await administration.QueueDeleteAsync(deadLetterRoutingKey.Value, false, false);
        }
    }
}
