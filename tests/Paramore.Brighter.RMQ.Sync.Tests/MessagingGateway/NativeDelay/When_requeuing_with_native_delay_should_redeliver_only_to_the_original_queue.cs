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
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.NativeDelay;

[Trait("Category", "RMQNativeDelay")]
[Trait("Requires", "RabbitMQDelayedPlugin")]
[Collection("RMQNativeDelay")]
public class RmqNativeDelayTests : IDisposable
{
    private static readonly Uri s_brokerUri = new(
        Environment.GetEnvironmentVariable("RMQ_NATIVE_DELAY_URI") ?? "amqp://guest:guest@localhost:5673/%2f");
    private readonly string _exchangeName = $"brighter.native-delay.{Guid.NewGuid():N}";
    private readonly ChannelName _queueName = new(Guid.NewGuid().ToString());
    private readonly ChannelName _otherQueueName = new(Guid.NewGuid().ToString());
    private readonly RoutingKey _topic = new("native.delay.original");
    private readonly IConnection _broker = new ConnectionFactory { Uri = s_brokerUri }.CreateConnection();

    [Theory]
    [InlineData(ExchangeType.Direct)]
    [InlineData(ExchangeType.Topic)]
    [InlineData(ExchangeType.Fanout)]
    public void When_requeuing_with_native_delay_should_redeliver_only_to_the_original_queue(
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
        var received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.False(received.IsEmpty);
        var otherReceived = Assert.Single(otherConsumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.False(otherReceived.IsEmpty);

        otherConsumer.Acknowledge(otherReceived);
        var delay = TimeSpan.FromSeconds(3);

        //Act
        var elapsed = Stopwatch.StartNew();
        Assert.True(consumer.Requeue(received, delay));

        //Assert
        var beforeDelay = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(1)));
        Assert.True(beforeDelay.IsEmpty);
        var redelivered = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.False(redelivered.IsEmpty);
        Assert.True(elapsed.Elapsed >= delay, $"Redelivered after {elapsed.Elapsed}, before {delay} elapsed.");

        new RmqMessageAssertion().Assert(message, redelivered);
        consumer.Acknowledge(redelivered);

        var otherRetry = Assert.Single(otherConsumer.Receive(TimeSpan.FromMilliseconds(500)));
        Assert.True(otherRetry.IsEmpty);
    }

    [Fact]
    public void When_sending_with_native_delay_should_clear_the_consumed_delay()
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
        var beforeDelay = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(1)));
        Assert.True(beforeDelay.IsEmpty);
        var received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.False(received.IsEmpty);
        Assert.True(elapsed.Elapsed >= delay, $"Delivered after {elapsed.Elapsed}, before {delay} elapsed.");
        Assert.Equal(TimeSpan.Zero, received.Header.Delayed);
        Assert.True(received.Header.Bag.TryGetValue("x-delay", out var rawDelay));
        Assert.Equal(-3000L, Convert.ToInt64(rawDelay));

        new RmqMessageAssertion().Assert(message, received);
        consumer.Acknowledge(received);
    }

    [Theory]
    [InlineData(ExchangeType.Direct)]
    [InlineData(ExchangeType.Topic)]
    [InlineData(ExchangeType.Fanout)]
    public void When_reusing_native_exchange_settings_should_allow_producer_and_consumer_to_connect(
        string exchangeType)
    {
        //Arrange
        var connection = CreateConnection(exchangeType);
        using var consumer = new RmqMessageConsumer(
            connection, _queueName, _topic, isDurable: true);

        using var producer = new RmqMessageProducer(connection);
        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();

        //Act
        var exception = Record.Exception(() =>
        {
            consumer.Purge();
            producer.Send(message);
        });

        //Assert
        Assert.Null(exception);
        Assert.Equal(exchangeType, connection.Exchange!.Type);

        var received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.False(received.IsEmpty);
        new RmqMessageAssertion().Assert(message, received);
        consumer.Acknowledge(received);
    }

    [Theory]
    [InlineData(OnMissingChannel.Validate)]
    [InlineData(OnMissingChannel.Assume)]
    public void When_using_preprovisioned_retry_topology_should_honor_native_delay(OnMissingChannel makeChannels)
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
        var received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.False(received.IsEmpty);

        //Act
        Assert.True(consumer.Requeue(received, TimeSpan.FromSeconds(3)));

        //Assert
        Assert.True(Assert.Single(consumer.Receive(TimeSpan.FromSeconds(1))).IsEmpty);
        var redelivered = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.False(redelivered.IsEmpty);
        new RmqMessageAssertion().Assert(message, redelivered);
        consumer.Acknowledge(redelivered);
    }

    [Theory]
    [InlineData(OnMissingChannel.Validate)]
    [InlineData(OnMissingChannel.Assume)]
    public void When_retry_topology_is_missing_should_not_create_it_without_permission(OnMissingChannel makeChannels)
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
        var exception = Record.Exception(() => consumer.Receive(TimeSpan.FromMilliseconds(100)));

        //Assert
        if (makeChannels == OnMissingChannel.Validate)
            Assert.IsType<ChannelFailureException>(exception);
        else
            Assert.Null(exception);
        Assert.Throws<RabbitMQ.Client.Exceptions.OperationInterruptedException>(() =>
            inspection.ExchangeDeclarePassive($"{_exchangeName}.requeue"));
    }

    [Fact]
    public void When_requeuing_with_negative_delay_should_redeliver_immediately()
    {
        //Arrange
        using var consumer = new RmqMessageConsumer(CreateConnection(ExchangeType.Direct), _queueName, _topic, isDurable: true);
        using var producer = new RmqMessageProducer(CreateConnection(ExchangeType.Direct));
        consumer.Purge();
        var message = new DefaultMessageBuilder().SetTopic(_topic).Build();
        producer.Send(message);
        var received = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(5)));
        Assert.False(received.IsEmpty);

        //Act
        Assert.True(consumer.Requeue(received, TimeSpan.FromSeconds(-1)));

        //Assert
        var redelivered = Assert.Single(consumer.Receive(TimeSpan.FromSeconds(1)));
        Assert.False(redelivered.IsEmpty);
        new RmqMessageAssertion().Assert(message, redelivered);
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
