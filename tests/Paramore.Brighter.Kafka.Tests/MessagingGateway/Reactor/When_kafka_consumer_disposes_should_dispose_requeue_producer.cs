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
using System.Threading.Tasks;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

/// <summary>
/// When the Kafka consumer is disposed, it should dispose the lazily-created requeue producer.
/// If no requeue was ever performed, disposal should succeed without error.
/// </summary>
[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaConsumerDisposesRequeueProducerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly string _channelName = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAMessageConsumerSync _consumer;

    public KafkaConsumerDisposesRequeueProducerTests(ITestOutputHelper output)
    {
        string groupId = Guid.NewGuid().ToString();
        _output = output;

        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Dispose Test",
                BootStrapServers = new[] { "localhost:9092" }
            },
            [
                new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 1,
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }
            ]).Create();

        _consumer = new KafkaMessageConsumerFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Consumer Dispose Test",
                BootStrapServers = new[] { "localhost:9092" }
            })
            .Create(new KafkaSubscription<MyCommand>(
                channelName: new ChannelName(_channelName),
                routingKey: new RoutingKey(_topic),
                groupId: groupId,
                numOfPartitions: 1,
                replicationFactor: 1,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create
            ));
    }

    [Fact]
    public void When_consumer_requeues_then_disposes_should_dispose_producer()
    {
        // Arrange - send a message and receive it to have something to requeue
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(_topic), MessageType.MT_COMMAND),
            new MessageBody("test content for dispose"));

        var producer = (IAmAMessageProducerSync)_producerRegistry.LookupBy(new RoutingKey(_topic));
        producer.Send(message);
        ((KafkaMessageProducer)producer).Flush();

        var received = GetMessage();
        Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

        // Act - requeue to trigger lazy producer creation, then dispose
        _consumer.Requeue(received);

        // Assert - disposing should not throw (producer cleanup succeeds)
        var exception = Record.Exception(() => _consumer.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void When_consumer_disposes_without_requeue_should_not_throw()
    {
        // Act + Assert - disposing without ever requeuing should succeed
        var exception = Record.Exception(() => _consumer.Dispose());
        Assert.Null(exception);
    }

    private Message GetMessage()
    {
        Message[] messages = [];
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                messages = _consumer.Receive(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                {
                    _consumer.Acknowledge(messages[0]);
                    break;
                }
            }
            catch (ChannelFailureException cfx)
            {
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                Task.Delay(1000).GetAwaiter().GetResult();
            }
        } while (maxTries <= 10);

        if (messages[0].Header.MessageType == MessageType.MT_NONE)
            throw new Exception($"Failed to read from topic:{_topic} after {maxTries} attempts");

        return messages[0];
    }

    public void Dispose()
    {
        _producerRegistry?.Dispose();
        // Consumer may already be disposed by the test, so catch any double-dispose
        try { _consumer?.Dispose(); } catch { /* already disposed */ }
    }
}
