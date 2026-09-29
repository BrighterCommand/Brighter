#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Net;
using System.Threading.Tasks;
using MQTTnet;
using Paramore.Brighter.MessagingGateway.MQTT;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Server;
using Paramore.Brighter.Tasks;


namespace Paramore.Brighter.MQTT.Tests.MessagingGateway;

/// <summary>
/// Issue #4351: <see cref="MqttMessagePublisher"/>'s constructor blocks on
/// <c>ConnectAsync().GetAwaiter().GetResult()</c>. The Proactor's pump runs its event loop inside
/// <see cref="BrighterAsyncContext"/>, a single-threaded context that pairs a
/// <see cref="System.Threading.SynchronizationContext"/> with a cooperating scheduler on one thread.
/// <see cref="MqttMessageConsumer"/> constructs its requeue and rejection producers lazily, on that
/// same pump thread, the first time a handler defers or rejects a message. Constructing the publisher
/// there means its blocking connect captures the pump's own context, so the connect's continuation is
/// posted back to the queue only the (now blocked) pump thread can drain - a permanent deadlock.
/// </summary>
[Property("Category", "MQTT")]
[System.Obsolete]
public class MqttPublisherPumpContextConstructionTests : IDisposable
{
    private readonly MqttTestServer? _mqttTestServer;

    public MqttPublisherPumpContextConstructionTests()
    {
        int serverPort = MqttTestServer.GetRandomServerPort();

        _mqttTestServer = MqttTestServer.CreateTestMqttServer(
            new MqttFactory(), true, null,
            IPAddress.Loopback, serverPort, null, nameof(MqttPublisherPumpContextConstructionTests));
    }

    [Test]
    public async Task When_a_publisher_is_constructed_inside_the_pump_context_should_not_deadlock()
    {
        // Arrange - a real broker (an embedded MQTTnet server on a random loopback port, so this
        // needs no docker-compose broker), and the same single-threaded pump context the Proactor
        // runs its event loop inside.
        var config = new MqttMessagingGatewayProducerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = _mqttTestServer!.ServerPort,
            TopicPrefix = "BrighterIntegrationTests/PumpContextConstruction",
            ClientID = "brighter-pump-context-construction-test"
        };

        // Act - construct the publisher from the pump thread, the way EnsureRequeueProducer does
        // when a Proactor handler defers or rejects a message.
        var construction = Task.Run(() => BrighterAsyncContext.Run(() =>
        {
            var publisher = new MqttMessagePublisher(config);
            return Task.FromResult(publisher);
        }));

        var completed = await Task.WhenAny(construction, Task.Delay(TimeSpan.FromSeconds(10)));

        // Assert - constructing the publisher must complete; today it never does, because the
        // constructor's blocking connect deadlocks against the pump thread it runs on.
        await Assert.That(completed).IsSameReferenceAs(construction);

        (await construction).Dispose();
    }

    public void Dispose()
    {
        _mqttTestServer?.Dispose();
    }
}
