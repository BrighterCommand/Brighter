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
using System.Diagnostics;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using RabbitMQ.Client;
using System.Threading.Tasks;
namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.NativeDelay;

[Property("Category", "RMQNativeDelay")]
[Property("Requires", "RabbitMQDelayedPlugin")]
[NotInParallel("RMQNativeDelay")]
public class RmqNativeDelayTests : IDisposable
{
    private static readonly Uri s_brokerUri = new(
        Environment.GetEnvironmentVariable("RMQ_NATIVE_DELAY_URI") ?? "amqp://guest:guest@localhost:5673/%2f");
    private readonly string _exchangeName = $"brighter.native-delay.{Guid.NewGuid():N}";
    private readonly ChannelName _queueName = new(Guid.NewGuid().ToString());
    private readonly ChannelName _otherQueueName = new(Guid.NewGuid().ToString());
    private readonly RoutingKey _topic = new("native.delay.original");
    private readonly IConnection _broker = new ConnectionFactory { Uri = s_brokerUri }.CreateConnection();

    [Test]
    [Arguments(ExchangeType.Direct)]
    [Arguments(ExchangeType.Topic)]
    [Arguments(ExchangeType.Fanout)]
    public async Task When_requeuing_with_native_delay_should_redeliver_only_to_the_original_queue(
        string exchangeType)
    {
        //Arrange
        using var consumer = new RmqMessageConsumer(
            CreateConnection(exchangeType), _queueName, _topic, isDurable: true);
        using var otherConsumer = new RmqMessageConsumer(
            CreateConnection(exchangeType), _otherQueueName, OtherRoutingKey(exchangeType), isDurable: true);
        using var producer = new RmqMessageProducer(CreateConnection(exchangeType));

        consumer.Purge();
        otherConsumer.Purge();

        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();
        producer.Send(message);
        var received = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.IsEmpty).IsFalse();
        var otherReceived = await Assert.That(otherConsumer.Receive(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(otherReceived.IsEmpty).IsFalse();

        otherConsumer.Acknowledge(otherReceived);
        var delay = TimeSpan.FromSeconds(3);

        //Act
        var elapsed = Stopwatch.StartNew();
        await Assert.That(consumer.Requeue(received, delay)).IsTrue();

        //Assert
        var beforeDelay = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(1))).HasSingleItem();
        await Assert.That(beforeDelay.IsEmpty).IsTrue();
        var redelivered = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(redelivered.IsEmpty).IsFalse();
        await Assert.That(elapsed.Elapsed >= delay).IsTrue().Because($"Redelivered after {elapsed.Elapsed}, before {delay} elapsed.");

        await new RmqMessageAssertion().AssertAsync(message, redelivered);
        consumer.Acknowledge(redelivered);

        var otherRetry = await Assert.That(otherConsumer.Receive(TimeSpan.FromMilliseconds(500))).HasSingleItem();
        await Assert.That(otherRetry.IsEmpty).IsTrue();
    }

    [Test]
    public async Task When_sending_with_native_delay_should_clear_the_consumed_delay()
    {
        //Arrange
        using var consumer = new RmqMessageConsumer(
            CreateConnection(ExchangeType.Direct), _queueName, _topic, isDurable: true);
        using var producer = new RmqMessageProducer(CreateConnection(ExchangeType.Direct));
        consumer.Purge();

        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();
        var delay = TimeSpan.FromSeconds(3);

        //Act
        var elapsed = Stopwatch.StartNew();
        producer.SendWithDelay(message, delay);

        //Assert
        var beforeDelay = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(1))).HasSingleItem();
        await Assert.That(beforeDelay.IsEmpty).IsTrue();
        var received = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.IsEmpty).IsFalse();
        await Assert.That(elapsed.Elapsed >= delay).IsTrue().Because($"Delivered after {elapsed.Elapsed}, before {delay} elapsed.");
        await Assert.That(received.Header.Delayed).IsEqualTo(TimeSpan.Zero);
        await Assert.That(received.Header.Bag.TryGetValue("x-delay", out var rawDelay)).IsTrue();
        await Assert.That(Convert.ToInt64(rawDelay)).IsEqualTo(-3000L);

        await new RmqMessageAssertion().AssertAsync(message, received);
        consumer.Acknowledge(received);
    }

    [Test]
    [Arguments(ExchangeType.Direct)]
    [Arguments(ExchangeType.Topic)]
    [Arguments(ExchangeType.Fanout)]
    public async Task When_reusing_native_exchange_settings_should_allow_producer_and_consumer_to_connect(
        string exchangeType)
    {
        //Arrange
        var connection = CreateConnection(exchangeType);
        using var consumer = new RmqMessageConsumer(
            connection, _queueName, _topic, isDurable: true);

        using var producer = new RmqMessageProducer(connection);
        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();

        //Act
        Exception? exception = null;
        try
        {
            consumer.Purge();
            producer.Send(message);
        }
        catch (Exception e) { exception = e; }

        //Assert
        await Assert.That(exception).IsNull();
        await Assert.That(connection.Exchange!.Type).IsEqualTo(exchangeType);

        var received = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.IsEmpty).IsFalse();
        await new RmqMessageAssertion().AssertAsync(message, received);
        consumer.Acknowledge(received);
    }

    [Test]
    [Arguments(OnMissingChannel.Validate)]
    [Arguments(OnMissingChannel.Assume)]
    public async Task When_using_preprovisioned_retry_topology_should_honor_native_delay(OnMissingChannel makeChannels)
    {
        //Arrange
        using (var setup = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic, isDurable: true))
        {
            setup.Purge();
        }
        using var consumer = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic,
            isDurable: true, makeChannels: makeChannels);
        using var producer = new RmqMessageProducer(CreateConnection(ExchangeType.Direct));
        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();
        producer.Send(message);
        var received = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.IsEmpty).IsFalse();

        //Act
        await Assert.That(consumer.Requeue(received, TimeSpan.FromSeconds(3))).IsTrue();

        //Assert
        await Assert.That((await Assert.That(consumer.Receive(TimeSpan.FromSeconds(1))).HasSingleItem()).IsEmpty).IsTrue();
        var redelivered = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(redelivered.IsEmpty).IsFalse();
        await new RmqMessageAssertion().AssertAsync(message, redelivered);
        consumer.Acknowledge(redelivered);
    }

    [Test]
    [Arguments(OnMissingChannel.Validate)]
    [Arguments(OnMissingChannel.Assume)]
    public async Task When_retry_topology_is_missing_should_not_create_it_without_permission(OnMissingChannel makeChannels)
    {
        //Arrange
        using (var setup = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic, isDurable: true))
        {
            setup.Purge();
        }
        using var inspection = _broker.CreateModel();
        inspection.ExchangeDelete($"{_exchangeName}.requeue");
        using var consumer = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic,
            isDurable: true, makeChannels: makeChannels);

        //Act
        Exception? exception = null;
        try { consumer.Receive(TimeSpan.FromMilliseconds(100)); } catch (Exception e) { exception = e; }

        //Assert
        if (makeChannels == OnMissingChannel.Validate)
            await Assert.That(exception).IsTypeOf<ChannelFailureException>();
        else
            await Assert.That(exception).IsNull();
        await Assert.That(() =>
            inspection.ExchangeDeclarePassive($"{_exchangeName}.requeue")).ThrowsExactly<RabbitMQ.Client.Exceptions.OperationInterruptedException>();
    }

    [Test]
    public async Task When_requeuing_with_negative_delay_should_redeliver_immediately()
    {
        //Arrange
        using var consumer = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic, isDurable: true);
        using var producer = new RmqMessageProducer(CreateConnection(ExchangeType.Direct));
        consumer.Purge();
        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();
        producer.Send(message);
        var received = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.IsEmpty).IsFalse();

        //Act
        await Assert.That(consumer.Requeue(received, TimeSpan.FromSeconds(-1))).IsTrue();

        //Assert
        var redelivered = await Assert.That(consumer.Receive(TimeSpan.FromSeconds(1))).HasSingleItem();
        await Assert.That(redelivered.IsEmpty).IsFalse();
        await new RmqMessageAssertion().AssertAsync(message, redelivered);
        consumer.Acknowledge(redelivered);
    }

    private RmqMessagingGatewayConnection CreateConnection(string exchangeType) => new()
    {
        AmpqUri = new AmqpUriSpecification(s_brokerUri),
        Exchange = new Exchange(_exchangeName, exchangeType, durable: true, supportDelay: true)
    };

    private RoutingKey OtherRoutingKey(string exchangeType) => exchangeType switch
    {
        ExchangeType.Topic => new RoutingKey("#"),
        ExchangeType.Fanout => new RoutingKey("another.topic"),
        _ => _topic
    };

    public void Dispose()
    {
        if (_broker == null)
            return;

        using (_broker)
        {
            using var channel = _broker.CreateModel();
            channel.QueueDelete(_queueName.Value);
            channel.QueueDelete(_otherQueueName.Value);
            channel.ExchangeDelete(_exchangeName);
            channel.ExchangeDelete($"{_exchangeName}.requeue");
        }
    }
}
