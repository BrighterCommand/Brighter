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

public class NatsNamingTests
{
    [Fact]
    public void When_resolving_subject_from_publication_topic_should_use_topic()
    {
        var publication = new NatsPublication { Topic = new RoutingKey("orders") };

        var subject = NatsNaming.ResolveSubject(publication);

        subject.ShouldBe("orders");
    }

    [Fact]
    public void When_resolving_subject_from_publication_override_should_use_override()
    {
        var publication = new NatsPublication
        {
            Topic = new RoutingKey("orders"),
            SubjectOverride = "events.orders"
        };

        var subject = NatsNaming.ResolveSubject(publication);

        subject.ShouldBe("events.orders");
    }

    [Fact]
    public void When_publication_has_no_topic_or_override_should_throw()
    {
        var publication = new NatsPublication();

        Should.Throw<ConfigurationException>(() => NatsNaming.ResolveSubject(publication));
    }

    [Fact]
    public void When_resolving_subject_from_subscription_routing_key_should_use_routing_key()
    {
        var subscription = new NatsSubscription<MyCommand>(
            routingKey: new RoutingKey("orders"));

        var subject = NatsNaming.ResolveSubject(subscription);

        subject.ShouldBe("orders");
    }

    [Fact]
    public void When_resolving_subject_from_subscription_override_should_use_override()
    {
        var subscription = new NatsSubscription<MyCommand>(
            routingKey: new RoutingKey("orders"),
            subjectOverride: "events.orders");

        var subject = NatsNaming.ResolveSubject(subscription);

        subject.ShouldBe("events.orders");
    }

    [Fact]
    public void When_resolving_consumer_name_from_channel_name_should_use_channel_name()
    {
        var subscription = new NatsSubscription<MyCommand>(
            channelName: new ChannelName("my-consumer"),
            routingKey: new RoutingKey("orders"));

        var name = NatsNaming.ResolveConsumerName(subscription);

        name.ShouldBe("my-consumer");
    }

    [Fact]
    public void When_resolving_consumer_name_from_override_should_use_override()
    {
        var subscription = new NatsSubscription<MyCommand>(
            channelName: new ChannelName("my-consumer"),
            routingKey: new RoutingKey("orders"),
            consumerName: "explicit-consumer");

        var name = NatsNaming.ResolveConsumerName(subscription);

        name.ShouldBe("explicit-consumer");
    }

    [Fact]
    public void When_subscription_has_no_channel_name_or_consumer_name_should_throw()
    {
        var subscription = new NatsSubscription<MyCommand>(
            channelName: new ChannelName(string.Empty),
            routingKey: new RoutingKey("orders"));

        Should.Throw<ConfigurationException>(() => NatsNaming.ResolveConsumerName(subscription));
    }
}
