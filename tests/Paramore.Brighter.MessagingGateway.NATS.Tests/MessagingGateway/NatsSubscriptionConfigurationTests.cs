#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using Paramore.Brighter.MessagingGateway.NATS;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.MessagingGateway.NATS.Tests.MessagingGateway;

public class NatsSubscriptionConfigurationTests
{
    [Fact]
    public void When_creating_subscription_should_expose_stream_and_subject_overrides()
    {
        var subscription = new NatsSubscription<MyCommand>(
            channelName: new ChannelName("my-consumer"),
            routingKey: new RoutingKey("orders"),
            streamName: "ORDERS",
            subjectOverride: "events.orders",
            consumerName: "explicit-consumer");

        subscription.StreamName.ShouldBe("ORDERS");
        subscription.SubjectOverride.ShouldBe("events.orders");
        subscription.ConsumerName.ShouldBe("explicit-consumer");
    }

    [Fact]
    public void When_creating_subscription_with_dlq_routing_keys_should_expose_them()
    {
        var dlq = new RoutingKey("orders-dlq");
        var invalid = new RoutingKey("orders-invalid");

        var subscription = new NatsSubscription<MyCommand>(
            channelName: new ChannelName("my-consumer"),
            routingKey: new RoutingKey("orders"),
            deadLetterRoutingKey: dlq,
            invalidMessageRoutingKey: invalid);

        subscription.DeadLetterRoutingKey.ShouldBe(dlq);
        subscription.InvalidMessageRoutingKey.ShouldBe(invalid);
    }

    [Fact]
    public void When_creating_subscription_without_dlq_routing_keys_should_default_to_null()
    {
        var subscription = new NatsSubscription<MyCommand>(
            channelName: new ChannelName("my-consumer"),
            routingKey: new RoutingKey("orders"));

        subscription.DeadLetterRoutingKey.ShouldBeNull();
        subscription.InvalidMessageRoutingKey.ShouldBeNull();
    }

    [Fact]
    public void When_creating_subscription_should_use_nats_channel_factory()
    {
        var subscription = new NatsSubscription<MyCommand>(
            channelName: new ChannelName("my-consumer"),
            routingKey: new RoutingKey("orders"));

        subscription.ChannelFactoryType.ShouldBe(typeof(ChannelFactory));
    }
}
