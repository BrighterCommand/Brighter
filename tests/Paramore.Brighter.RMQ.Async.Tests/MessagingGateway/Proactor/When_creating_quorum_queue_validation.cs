#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Trait("Category", "RMQ")]
public class RmqMessageConsumerQuorumValidationTests
{
    [Fact]
    public void When_creating_quorum_consumer_without_durability_should_throw()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.durableexchange", durable: true)
        };

        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        var exception = Assert.Throws<ConfigurationException>(() =>
            new RmqMessageConsumer(rmqConnection, queueName, routingKey,
                isDurable: false, // This should cause the exception
                highAvailability: false,
                queueType: QueueType.Quorum));

        Assert.Contains("Quorum queues require durability to be enabled", exception.Message);
    }

    [Fact]
    public void When_creating_quorum_consumer_with_high_availability_should_throw()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        var exception = Assert.Throws<ConfigurationException>(() =>
            new RmqMessageConsumer(rmqConnection, queueName, routingKey,
                isDurable: true,
                highAvailability: true, // This should cause the exception
                queueType: QueueType.Quorum));

        Assert.Contains("Quorum queues do not support high availability mirroring", exception.Message);
    }

    [Fact]
    public void When_creating_quorum_consumer_with_correct_settings_should_succeed()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        // This should not throw any exception
        using var consumer = new RmqMessageConsumer(rmqConnection, queueName, routingKey,
            isDurable: true, // Required for quorum
            highAvailability: false, // Must be false for quorum
            queueType: QueueType.Quorum);

        Assert.NotNull(consumer);
    }

    [Fact]
    public void When_creating_classic_consumer_with_default_settings_should_succeed()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.durableexchange", durable: true)
        };

        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        // Classic queue (default) should work with any settings
        using var consumer = new RmqMessageConsumer(rmqConnection, queueName, routingKey,
            isDurable: false,
            highAvailability: true,
            queueType: QueueType.Classic);

        new QueueFactory(rmqConnection, queueName, new RoutingKeys(routingKey), isDurable: true)
            .CreateAsync()
            .GetAwaiter()
            .GetResult();

        Assert.NotNull(consumer);
    }
}