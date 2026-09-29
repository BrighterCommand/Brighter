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
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using RabbitMQ.Client;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.NativeDelay;

[Property("Category", "RMQNativeDelay")]
[Property("Requires", "RabbitMQDelayedPlugin")]
[NotInParallel("RMQNativeDelay")]
public class RmqNativeDelayTests
{
    private static readonly Uri s_brokerUri = new(
        Environment.GetEnvironmentVariable("RMQ_NATIVE_DELAY_URI") ?? "amqp://guest:guest@localhost:5673/%2f");
    private readonly string _exchangeName = $"brighter.native-delay.{Guid.NewGuid():N}";
    private readonly ChannelName _queueName = new(Guid.NewGuid().ToString());
    private readonly ChannelName _otherQueueName = new(Guid.NewGuid().ToString());
    private readonly RoutingKey _topic = new("native.delay.original");
    private IConnection _broker = null!;

    [Before(HookType.Test)]
    public async Task InitializeAsync()
    {
        _broker = await new ConnectionFactory { Uri = s_brokerUri }.CreateConnectionAsync();
    }

    [Test]
    [Arguments(QueueType.Classic, ExchangeType.Direct)]
    [Arguments(QueueType.Classic, ExchangeType.Topic)]
    [Arguments(QueueType.Classic, ExchangeType.Fanout)]
    [Arguments(QueueType.Quorum, ExchangeType.Direct)]
    [Arguments(QueueType.Quorum, ExchangeType.Topic)]
    [Arguments(QueueType.Quorum, ExchangeType.Fanout)]
    public async Task When_requeuing_with_native_delay_should_redeliver_only_to_the_original_queue(
        QueueType queueType, string exchangeType)
    {
        //Arrange
        await using var consumer = new RmqMessageConsumer(
            CreateConnection(exchangeType), _queueName, _topic, isDurable: true, queueType: queueType);
        await using var otherConsumer = new RmqMessageConsumer(
            CreateConnection(exchangeType), _otherQueueName, OtherRoutingKey(exchangeType), isDurable: true,
            queueType: queueType);
        await using var producer = new RmqMessageProducer(CreateConnection(exchangeType));
        await consumer.PurgeAsync();
        await otherConsumer.PurgeAsync();

        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();
        await producer.SendAsync(message);
        var received = await Assert.That(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.IsEmpty).IsFalse();
        var otherReceived = await Assert.That(await otherConsumer.ReceiveAsync(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(otherReceived.IsEmpty).IsFalse();
        await otherConsumer.AcknowledgeAsync(otherReceived);
        var delay = TimeSpan.FromSeconds(3);

        //Act
        var elapsed = Stopwatch.StartNew();
        await Assert.That(await consumer.RequeueAsync(received, delay)).IsTrue();

        //Assert
        var beforeDelay = await Assert.That(await consumer.ReceiveAsync(TimeSpan.FromSeconds(1))).HasSingleItem();
        await Assert.That(beforeDelay.IsEmpty).IsTrue();
        var redelivered = await Assert.That(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(redelivered.IsEmpty).IsFalse();
        await Assert.That(elapsed.Elapsed >= delay).IsTrue().Because($"Redelivered after {elapsed.Elapsed}, before {delay} elapsed.");
        await new RmqMessageAssertion().AssertAsync(message, redelivered);
        await consumer.AcknowledgeAsync(redelivered);

        var otherRetry = await Assert.That(await otherConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(500))).HasSingleItem();
        await Assert.That(otherRetry.IsEmpty).IsTrue();
    }

    [Test]
    [Arguments(QueueType.Classic)]
    [Arguments(QueueType.Quorum)]
    public async Task When_sending_with_native_delay_should_clear_the_consumed_delay(QueueType queueType)
    {
        //Arrange
        await using var consumer = new RmqMessageConsumer(
            CreateConnection(ExchangeType.Direct), _queueName, _topic, isDurable: true, queueType: queueType);
        await using var producer = new RmqMessageProducer(CreateConnection(ExchangeType.Direct));
        await consumer.PurgeAsync();
        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();
        var delay = TimeSpan.FromSeconds(3);

        //Act
        var elapsed = Stopwatch.StartNew();
        await producer.SendWithDelayAsync(message, delay);

        //Assert
        var beforeDelay = await Assert.That(await consumer.ReceiveAsync(TimeSpan.FromSeconds(1))).HasSingleItem();
        await Assert.That(beforeDelay.IsEmpty).IsTrue();
        var received = await Assert.That(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.IsEmpty).IsFalse();
        await Assert.That(elapsed.Elapsed >= delay).IsTrue().Because($"Delivered after {elapsed.Elapsed}, before {delay} elapsed.");
        await Assert.That(received.Header.Delayed).IsEqualTo(TimeSpan.Zero);
        await Assert.That(received.Header.Bag.TryGetValue("x-delay", out var rawDelay)).IsTrue();
        await Assert.That(Convert.ToInt64(rawDelay)).IsEqualTo(-3000L);
        await new RmqMessageAssertion().AssertAsync(message, received);
        await consumer.AcknowledgeAsync(received);
    }

    [Test]
    [Arguments(QueueType.Classic, ExchangeType.Direct)]
    [Arguments(QueueType.Classic, ExchangeType.Topic)]
    [Arguments(QueueType.Classic, ExchangeType.Fanout)]
    [Arguments(QueueType.Quorum, ExchangeType.Direct)]
    [Arguments(QueueType.Quorum, ExchangeType.Topic)]
    [Arguments(QueueType.Quorum, ExchangeType.Fanout)]
    public async Task When_reusing_native_exchange_settings_should_allow_producer_and_consumer_to_connect(
        QueueType queueType, string exchangeType)
    {
        //Arrange
        var connection = CreateConnection(exchangeType);
        await using var consumer = new RmqMessageConsumer(
            connection, _queueName, _topic, isDurable: true, queueType: queueType);
        await using var producer = new RmqMessageProducer(connection);
        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(async () =>
        {
            await consumer.PurgeAsync();
            await producer.SendAsync(message);
        });

        //Assert
        await Assert.That(exception).IsNull();
        await Assert.That(connection.Exchange!.Type).IsEqualTo(exchangeType);
        var received = await Assert.That(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.IsEmpty).IsFalse();
        await new RmqMessageAssertion().AssertAsync(message, received);
        await consumer.AcknowledgeAsync(received);
    }

    [Test]
    [Arguments(OnMissingChannel.Validate)]
    [Arguments(OnMissingChannel.Assume)]
    public async Task When_using_preprovisioned_retry_topology_should_honor_native_delay(OnMissingChannel makeChannels)
    {
        //Arrange
        await using (var setup = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic, isDurable: true))
        {
            await setup.PurgeAsync();
        }
        await using var consumer = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic,
            isDurable: true, makeChannels: makeChannels);
        await using var producer = new RmqMessageProducer(CreateConnection(ExchangeType.Direct));
        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();
        await producer.SendAsync(message);
        var received = await Assert.That(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.IsEmpty).IsFalse();

        //Act
        await Assert.That(await consumer.RequeueAsync(received, TimeSpan.FromSeconds(3))).IsTrue();

        //Assert
        await Assert.That((await Assert.That(await consumer.ReceiveAsync(TimeSpan.FromSeconds(1))).HasSingleItem()).IsEmpty).IsTrue();
        var redelivered = await Assert.That(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(redelivered.IsEmpty).IsFalse();
        await new RmqMessageAssertion().AssertAsync(message, redelivered);
        await consumer.AcknowledgeAsync(redelivered);
    }

    [Test]
    [Arguments(OnMissingChannel.Validate)]
    [Arguments(OnMissingChannel.Assume)]
    public async Task When_retry_topology_is_missing_should_not_create_it_without_permission(OnMissingChannel makeChannels)
    {
        //Arrange
        await using (var setup = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic, isDurable: true))
        {
            await setup.PurgeAsync();
        }
        await using var inspection = await _broker.CreateChannelAsync();
        await inspection.ExchangeDeleteAsync($"{_exchangeName}.requeue");
        await using var consumer = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic,
            isDurable: true, makeChannels: makeChannels);

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(async () => await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(100)));

        //Assert
        if (makeChannels == OnMissingChannel.Validate)
            await Assert.That(exception).IsTypeOf<ChannelFailureException>();
        else
            await Assert.That(exception).IsNull();
        await Assert.That(async () =>
            await inspection.ExchangeDeclarePassiveAsync($"{_exchangeName}.requeue")).ThrowsExactly<RabbitMQ.Client.Exceptions.OperationInterruptedException>();
    }

    [Test]
    public async Task When_requeuing_with_negative_delay_should_redeliver_immediately()
    {
        //Arrange
        await using var consumer = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic, isDurable: true);
        await using var producer = new RmqMessageProducer(CreateConnection(ExchangeType.Direct));
        await consumer.PurgeAsync();
        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();
        await producer.SendAsync(message);
        var received = await Assert.That(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(received.IsEmpty).IsFalse();

        //Act
        await Assert.That(await consumer.RequeueAsync(received, TimeSpan.FromSeconds(-1))).IsTrue();

        //Assert
        var redelivered = await Assert.That(await consumer.ReceiveAsync(TimeSpan.FromSeconds(1))).HasSingleItem();
        await Assert.That(redelivered.IsEmpty).IsFalse();
        await new RmqMessageAssertion().AssertAsync(message, redelivered);
        await consumer.AcknowledgeAsync(redelivered);
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

    [After(HookType.Test)]
    public async Task DisposeAsync()
    {
        if (_broker == null)
            return;

        await using (_broker)
        {
            await using var channel = await _broker.CreateChannelAsync();
            await channel.QueueDeleteAsync(_queueName.Value);
            await channel.QueueDeleteAsync(_otherQueueName.Value);
            await channel.ExchangeDeleteAsync(_exchangeName);
            await channel.ExchangeDeleteAsync($"{_exchangeName}.requeue");
        }
    }
}
