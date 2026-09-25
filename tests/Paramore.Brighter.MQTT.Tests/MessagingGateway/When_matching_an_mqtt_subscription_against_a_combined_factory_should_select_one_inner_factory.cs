#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Linq;
using Paramore.Brighter.MessagingGateway.MQTT;
using Paramore.Brighter.MQTT.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway;

public class MqttCombinedChannelFactoryRoutingTests
{
    [Fact]
    public void When_matching_an_mqtt_subscription_against_a_combined_factory_should_select_one_inner_factory()
    {
        // Arrange — construction only, so no broker connection is made (NFR-3): the MQTT ChannelFactory
        // wraps a consumer factory that only dials the broker when Create/CreateAsync is called
        var configuration = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = "localhost",
            Port = 1883,
            TopicPrefix = "test",
            ClientID = "test-client"
        };
        var consumerFactory = new MqttMessageConsumerFactory(configuration);
        var channelFactory = new ChannelFactory(consumerFactory);
        var combinedChannelFactory = new CombinedChannelFactory([channelFactory]);
        var subscription = new MqttSubscription<MyCommand>(
            new SubscriptionName("t"),
            new ChannelName("t"),
            new RoutingKey("t"));

        // Act — the routing decision CombinedChannelFactory would make, without creating a channel
        var matchingInnerFactories = combinedChannelFactory.FactoryTypes
            .Count(factoryType => factoryType == subscription.ChannelFactoryType);

        // Assert
        Assert.Equal(1, matchingInnerFactories);
    }
}
