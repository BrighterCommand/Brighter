#region Licence
/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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
using System.Collections.Generic;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using RabbitMQ.Client;
using System.Threading.Tasks;
namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Property("Category", "RMQ")]
[NotInParallel("RMQ")]
public class RmqQueueTtlTests
{
    [Test]
    [Arguments(0)]
    [Arguments(500)]
    [Arguments(1500)]
    [Arguments(30000)]
    [Arguments(int.MaxValue)]
    public async Task When_configuring_queue_ttl_should_use_total_milliseconds(int ttlMilliseconds)
    {
        //Arrange
        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var exchangeName = Guid.NewGuid().ToString();
        var amqpUri = new Uri("amqp://guest:guest@localhost:5672/%2f");
        var gatewayConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(amqpUri),
            Exchange = new Exchange(exchangeName)
        };

        var subscription = new RmqSubscription(
            subscriptionName: new SubscriptionName(Guid.NewGuid().ToString()),
            channelName: queueName,
            routingKey: new RoutingKey(Guid.NewGuid().ToString()),
            requestType: typeof(Command),
            ttl: TimeSpan.FromMilliseconds(ttlMilliseconds),
            makeChannels: OnMissingChannel.Create);

        using var connection = new ConnectionFactory { Uri = amqpUri }.CreateConnection();

        try
        {
            using var consumer = new RmqMessageConsumerFactory(gatewayConnection).Create(subscription);

            //Act
            consumer.Purge();

            //Assert
            using var channel = connection.CreateModel();
            channel.QueueDeclarePassive(queueName.Value);

            Exception? exception = null;
            try
            {
                channel.QueueDeclare(
                queueName.Value, durable: false, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object> { ["x-message-ttl"] = ttlMilliseconds });
            }
            catch (Exception e) { exception = e; }
            await Assert.That(exception).IsNull();
        }
        finally
        {
            using var cleanupChannel = connection.CreateModel();
            cleanupChannel.QueueDelete(queueName.Value);
            cleanupChannel.ExchangeDelete(exchangeName);
        }
    }
}
