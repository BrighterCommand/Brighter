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
using System.Reflection;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using Paramore.Brighter.MessagingGateway.MQTT;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Server;


namespace Paramore.Brighter.MQTT.Tests.MessagingGateway;

/// <summary>
/// The dead-letter and invalid-message producers are created lazily, on the first rejection that
/// needs one, and each owns an <see cref="MqttMessagePublisher"/> that opens its own broker
/// connection in its constructor.
/// </summary>
/// <remarks>
/// <para>
/// Disposing the consumer released <c>_requeueProducer</c> and the consumer's own client but not
/// these two, so any consumer that had rejected at least one message left a broker connection open
/// for the lifetime of the process. Nothing fails when that happens - the connections simply
/// accumulate - which is why it needs a test rather than a reader.
/// </para>
/// <para>
/// The second pair of tests guards the way the fix goes wrong: reaching through
/// <see cref="Lazy{T}.Value"/> without checking <see cref="Lazy{T}.IsValueCreated"/> would have
/// <c>Dispose</c> construct a producer, and so open a broker connection, purely in order to close
/// it - on every consumer that never rejected anything.
/// </para>
/// </remarks>
[Property("Category", "MQTT")]
[System.Obsolete]
public class MqttConsumerRejectionProducerDisposalTests : IDisposable
{
    private readonly MqttTestServer? _mqttTestServer;
    private readonly MqttMessagingGatewayConsumerConfiguration _configuration;

    public MqttConsumerRejectionProducerDisposalTests()
    {
        var serverPort = MqttTestServer.GetRandomServerPort();
        _mqttTestServer = MqttTestServer.CreateTestMqttServer(new MqttFactory(), true, serverPort: serverPort);

        _configuration = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = "BrighterTests/RejectionDisposal",
            ClientID = "BrighterTests-RejectionDisposal"
        };
    }

    private MqttMessageConsumer AConsumerWithRejectionRoutes() => new(
        _configuration,
        scheduler: null,
        deadLetterRoutingKey: new RoutingKey("orders-dlq"),
        invalidMessageRoutingKey: new RoutingKey("orders-invalid"));

    private static Lazy<MqttMessageProducer?>? LazyProducer(MqttMessageConsumer consumer, string fieldName)
        => consumer.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer) as Lazy<MqttMessageProducer?>;

    /// <summary>
    /// Reaches the broker connection the producer owns, which is what disposal has to release.
    /// </summary>
    /// <remarks>
    /// Asked whether it is <em>disposed</em>, not whether it is connected: MQTTnet leaves
    /// <see cref="IMqttClient.IsConnected"/> true after <c>Dispose</c> - only
    /// <c>DisposeAsync</c> disconnects first - so a connectedness check would report the sync
    /// path as leaking however it is disposed. Every operation on a disposed client throws,
    /// and that is true of both paths.
    /// </remarks>
    private static IMqttClient ClientOf(MqttMessageProducer producer)
    {
        var publisher = producer.GetType()
            .GetField("_mqttMessagePublisher", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(producer)!;

        return (IMqttClient)publisher.GetType()
            .GetField("_mqttClient", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(publisher)!;
    }

    /// <summary>Forces both lazy producers, as a rejection would, and returns their clients.</summary>
    private static async Task<(IMqttClient DeadLetter, IMqttClient Invalid)> ConnectRejectionProducers(
        MqttMessageConsumer consumer)
    {
        var deadLetter = LazyProducer(consumer, "_deadLetterProducer")!.Value;
        var invalid = LazyProducer(consumer, "_invalidMessageProducer")!.Value;

        // Non-vacuity: if the producers could not be built, "disconnected after Dispose" would
        // hold for a reason that has nothing to do with disposal.
        await Assert.That(deadLetter).IsNotNull();
        await Assert.That(invalid).IsNotNull();

        var clients = (ClientOf(deadLetter!), ClientOf(invalid!));
        await Assert.That(clients.Item1.IsConnected).IsTrue();
        await Assert.That(clients.Item2.IsConnected).IsTrue();
        return clients;
    }

    /// <summary>Whether the producer's broker connection has been released.</summary>
    private static async Task<bool> IsDisposed(IMqttClient client)
    {
        try
        {
            await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic("probe").Build());
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    [Test]
    public async Task When_disposing_a_consumer_should_dispose_its_rejection_producers()
    {
        //Arrange
        var consumer = AConsumerWithRejectionRoutes();
        var (deadLetterClient, invalidClient) = await ConnectRejectionProducers(consumer);

        //Act
        consumer.Dispose();

        //Assert
        await Assert.That(await IsDisposed(deadLetterClient)).IsTrue();
        await Assert.That(await IsDisposed(invalidClient)).IsTrue();
    }

    [Test]
    public async Task When_disposing_a_consumer_asynchronously_should_dispose_its_rejection_producers()
    {
        //Arrange
        var consumer = AConsumerWithRejectionRoutes();
        var (deadLetterClient, invalidClient) = await ConnectRejectionProducers(consumer);

        //Act
        await consumer.DisposeAsync();

        //Assert
        await Assert.That(await IsDisposed(deadLetterClient)).IsTrue();
        await Assert.That(await IsDisposed(invalidClient)).IsTrue();
    }

    [Test]
    public async System.Threading.Tasks.Task When_disposing_a_consumer_that_never_rejected_should_not_create_its_rejection_producers()
    {
        //Arrange
        var consumer = AConsumerWithRejectionRoutes();

        //Act
        consumer.Dispose();

        //Assert - disposal must not open a connection in order to close it
        await Assert.That(LazyProducer(consumer, "_deadLetterProducer")!.IsValueCreated).IsFalse();
        await Assert.That(LazyProducer(consumer, "_invalidMessageProducer")!.IsValueCreated).IsFalse();
    }

    [Test]
    public async Task When_disposing_a_consumer_that_never_rejected_asynchronously_should_not_create_its_rejection_producers()
    {
        //Arrange
        var consumer = AConsumerWithRejectionRoutes();

        //Act
        await consumer.DisposeAsync();

        //Assert
        await Assert.That(LazyProducer(consumer, "_deadLetterProducer")!.IsValueCreated).IsFalse();
        await Assert.That(LazyProducer(consumer, "_invalidMessageProducer")!.IsValueCreated).IsFalse();
    }

    public void Dispose() => _mqttTestServer?.Dispose();
}
