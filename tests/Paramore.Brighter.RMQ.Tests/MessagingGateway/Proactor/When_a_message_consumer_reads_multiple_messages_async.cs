using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessagingGateway.Proactor;

[Trait("Category", "RMQ")]
public class RMQBufferedConsumerTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly IAmAMessageConsumerAsync _messageConsumer;
    private readonly ChannelName _channelName = new(Guid.NewGuid().ToString());
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private const int BatchSize = 3;

    public RMQBufferedConsumerTestsAsync()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);
        _messageConsumer = new RmqMessageConsumer(connection:rmqConnection, queueName:_channelName, routingKey:_routingKey, isDurable:false, highAvailability:false, batchSize:BatchSize);

        //create the queue, so that we can receive messages posted to it
        new QueueFactory(rmqConnection, _channelName, new RoutingKeys(_routingKey)).CreateAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task When_a_message_consumer_reads_multiple_messages()
    {
        //Post one more than batch size messages
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content One"));
        await _messageProducer.SendAsync(messageOne);
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Two"));
        await _messageProducer.SendAsync(messageTwo);
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Three"));
        await _messageProducer.SendAsync(messageThree);
        var messageFour = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Four"));
        await _messageProducer.SendAsync(messageFour);

        //let them arrive
        await Task.Delay(5000);

        //Now retrieve messages from the consumer
        var messages = await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

        //We should only have three messages
        messages.Length.Should().Be(3);

        //ack those to remove from the queue
        foreach (var message in messages)
        {
            await _messageConsumer.AcknowledgeAsync(message);
        }

        //Allow ack to register
        await Task.Delay(1000);

        //Now retrieve again
        messages = await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(500));

        //This time, just the one message
        messages.Length.Should().Be(1);
    }

    public void Dispose()
    {
        _messageConsumer.PurgeAsync().GetAwaiter().GetResult();
        ((IAmAMessageProducerSync)_messageProducer).Dispose();
        ((IAmAMessageProducerSync)_messageProducer).Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _messageConsumer.PurgeAsync();
        await _messageProducer.DisposeAsync();
        await _messageConsumer.DisposeAsync();
    }
}
