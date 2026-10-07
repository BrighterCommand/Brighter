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
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.NativeDelay;

[Trait("Category", "RMQNativeDelay")]
[Trait("Requires", "RabbitMQDelayedPlugin")]
[Collection("RMQNativeDelay")]
public class RmqNativeDelayTests : IAsyncLifetime
{
    private static readonly Uri s_brokerUri = new(
        Environment.GetEnvironmentVariable("RMQ_NATIVE_DELAY_URI") ?? "amqp://guest:guest@localhost:5673/%2f");
    private readonly string _exchangeName = $"brighter.native-delay.{Guid.NewGuid():N}";
    private readonly ChannelName _queueName = new(Guid.NewGuid().ToString());
    private readonly ChannelName _otherQueueName = new(Guid.NewGuid().ToString());
    private readonly RoutingKey _topic = new("native.delay.original");
    private IConnection _broker = null!;

    public async Task InitializeAsync()
    {
        _broker = await new ConnectionFactory { Uri = s_brokerUri }.CreateConnectionAsync();
    }

    [Theory]
    [InlineData(QueueType.Classic, ExchangeType.Direct)]
    [InlineData(QueueType.Classic, ExchangeType.Topic)]
    [InlineData(QueueType.Classic, ExchangeType.Fanout)]
    [InlineData(QueueType.Quorum, ExchangeType.Direct)]
    [InlineData(QueueType.Quorum, ExchangeType.Topic)]
    [InlineData(QueueType.Quorum, ExchangeType.Fanout)]
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
        var received = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
        Assert.False(received.IsEmpty);
        var otherReceived = Assert.Single(await otherConsumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
        Assert.False(otherReceived.IsEmpty);
        await otherConsumer.AcknowledgeAsync(otherReceived);
        var delay = TimeSpan.FromSeconds(3);

        //Act
        var elapsed = Stopwatch.StartNew();
        Assert.True(await consumer.RequeueAsync(received, delay));

        //Assert
        var beforeDelay = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(1)));
        Assert.True(beforeDelay.IsEmpty);
        var redelivered = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
        Assert.False(redelivered.IsEmpty);
        Assert.True(elapsed.Elapsed >= delay, $"Redelivered after {elapsed.Elapsed}, before {delay} elapsed.");
        new RmqMessageAssertion().Assert(message, redelivered);
        await consumer.AcknowledgeAsync(redelivered);

        var otherRetry = Assert.Single(await otherConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(500)));
        Assert.True(otherRetry.IsEmpty);
    }

    [Theory]
    [InlineData(QueueType.Classic)]
    [InlineData(QueueType.Quorum)]
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
        var beforeDelay = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(1)));
        Assert.True(beforeDelay.IsEmpty);
        var received = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
        Assert.False(received.IsEmpty);
        Assert.True(elapsed.Elapsed >= delay, $"Delivered after {elapsed.Elapsed}, before {delay} elapsed.");
        Assert.Equal(TimeSpan.Zero, received.Header.Delayed);
        Assert.True(received.Header.Bag.TryGetValue("x-delay", out var rawDelay));
        Assert.Equal(-3000L, Convert.ToInt64(rawDelay));
        new RmqMessageAssertion().Assert(message, received);
        await consumer.AcknowledgeAsync(received);
    }

    [Theory]
    [InlineData(QueueType.Classic, ExchangeType.Direct)]
    [InlineData(QueueType.Classic, ExchangeType.Topic)]
    [InlineData(QueueType.Classic, ExchangeType.Fanout)]
    [InlineData(QueueType.Quorum, ExchangeType.Direct)]
    [InlineData(QueueType.Quorum, ExchangeType.Topic)]
    [InlineData(QueueType.Quorum, ExchangeType.Fanout)]
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
        var exception = await Record.ExceptionAsync(async () =>
        {
            await consumer.PurgeAsync();
            await producer.SendAsync(message);
        });

        //Assert
        Assert.Null(exception);
        Assert.Equal(exchangeType, connection.Exchange!.Type);
        var received = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
        Assert.False(received.IsEmpty);
        new RmqMessageAssertion().Assert(message, received);
        await consumer.AcknowledgeAsync(received);
    }

    [Theory]
    [InlineData(OnMissingChannel.Validate)]
    [InlineData(OnMissingChannel.Assume)]
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
        var received = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
        Assert.False(received.IsEmpty);

        //Act
        Assert.True(await consumer.RequeueAsync(received, TimeSpan.FromSeconds(3)));

        //Assert
        Assert.True(Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(1))).IsEmpty);
        var redelivered = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
        Assert.False(redelivered.IsEmpty);
        new RmqMessageAssertion().Assert(message, redelivered);
        await consumer.AcknowledgeAsync(redelivered);
    }

    [Theory]
    [InlineData(OnMissingChannel.Validate)]
    [InlineData(OnMissingChannel.Assume)]
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
        var exception = await Record.ExceptionAsync(async () => await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(100)));

        //Assert
        if (makeChannels == OnMissingChannel.Validate)
            Assert.IsType<ChannelFailureException>(exception);
        else
            Assert.Null(exception);
        await Assert.ThrowsAsync<RabbitMQ.Client.Exceptions.OperationInterruptedException>(async () =>
            await inspection.ExchangeDeclarePassiveAsync($"{_exchangeName}.requeue"));
    }

    [Fact]
    public async Task When_requeuing_with_negative_delay_should_redeliver_immediately()
    {
        //Arrange
        await using var consumer = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic, isDurable: true);
        await using var producer = new RmqMessageProducer(CreateConnection(ExchangeType.Direct));
        await consumer.PurgeAsync();
        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();
        await producer.SendAsync(message);
        var received = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(5)));
        Assert.False(received.IsEmpty);

        //Act
        Assert.True(await consumer.RequeueAsync(received, TimeSpan.FromSeconds(-1)));

        //Assert
        var redelivered = Assert.Single(await consumer.ReceiveAsync(TimeSpan.FromSeconds(1)));
        Assert.False(redelivered.IsEmpty);
        new RmqMessageAssertion().Assert(message, redelivered);
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
