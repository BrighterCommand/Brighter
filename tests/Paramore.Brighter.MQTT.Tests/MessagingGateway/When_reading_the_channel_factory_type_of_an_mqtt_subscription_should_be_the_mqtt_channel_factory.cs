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

using Paramore.Brighter.MessagingGateway.MQTT;
using Paramore.Brighter.MQTT.Tests.TestDoubles;


namespace Paramore.Brighter.MQTT.Tests.MessagingGateway;

[Property("Category", "MQTT")]
public class MqttSubscriptionChannelFactoryTypeTests
{
    [Test]
    public async System.Threading.Tasks.Task When_reading_the_channel_factory_type_of_an_mqtt_subscription_should_be_the_mqtt_channel_factory()
    {
        // Arrange — no explicit messagePumpType is needed: MqttSubscription<T> already defaults to Proactor
        var subscription = new MqttSubscription<MyCommand>(
            new SubscriptionName("t"),
            new ChannelName("t"),
            new RoutingKey("t"));

        // Act
        var channelFactoryType = subscription.ChannelFactoryType;

        // Assert
        await Assert.That(channelFactoryType).IsEqualTo(typeof(ChannelFactory));
    }
}
