using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Fifo.Reactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class SqsBufferedConsumerTests : IDisposable, IAsyncDisposable
{
    private readonly SqsMessageProducer _messageProducer;
    private readonly SqsMessageConsumer _consumer;
    private readonly string _queueName;
    private readonly ChannelFactory _channelFactory;
    private readonly ContentType _contentType = new(MediaTypeNames.Text.Plain);
    private const int BufferSize = 3;
    private const int MessageCount = 4;

    public SqsBufferedConsumerTests()
    {
        var awsConnection = GatewayFactory.CreateFactory();
        _channelFactory = new ChannelFactory(awsConnection);

        var subscriptionName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _queueName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var routingKey = new RoutingKey(_queueName);
        var channelName = new ChannelName(_queueName);
        var queueAttributes = new SqsAttributes(
            type: SqsType.Fifo,
            contentBasedDeduplication: true,
            deduplicationScope: DeduplicationScope.MessageGroup,
            fifoThroughputLimit: FifoThroughputLimit.PerMessageGroupId
        );

        var channel = _channelFactory.CreateSyncChannel(new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            bufferSize: BufferSize,
            queueAttributes: queueAttributes,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(true), BufferSize);
        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication(
                channelName: channelName,
                queueAttributes: queueAttributes,
                makeChannels: OnMissingChannel.Create
            ));
    }

    [Fact]
    public async Task When_a_message_consumer_reads_multiple_messages()
    {
        var routingKey = new RoutingKey(_queueName);

        var messageGroupIdOne = $"MessageGroup{Guid.NewGuid():N}";
        var messageOne = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType, partitionKey: messageGroupIdOne),
            new MessageBody("test content one")
        );

        var messageTwo = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType, partitionKey: messageGroupIdOne),
            new MessageBody("test content two")
        );


        var messageGroupIdTwo = $"MessageGroup{Guid.NewGuid():N}";
        var deduplicationId = $"DeduplicationId{Guid.NewGuid():N}";

        var messageThree = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType, partitionKey: messageGroupIdTwo)
            {
                Bag = { [HeaderNames.DeduplicationId] = deduplicationId }
            },
            new MessageBody("test content three")
        );

        var messageFour = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType, partitionKey: messageGroupIdTwo)
            {
                Bag = { [HeaderNames.DeduplicationId] = deduplicationId }
            },
            new MessageBody("test content four")
        );

        var messageFive = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType, partitionKey: messageGroupIdTwo),
            new MessageBody("test content four")
        );

        //send MESSAGE_COUNT messages 
        _messageProducer.Send(messageOne);
        _messageProducer.Send(messageTwo);
        _messageProducer.Send(messageThree);
        _messageProducer.Send(messageFour);
        _messageProducer.Send(messageFive);


        int iteration = 0;
        var messagesReceived = new List<Message>();
        var messagesReceivedCount = messagesReceived.Count;
        do
        {
            iteration++;
            var outstandingMessageCount = MessageCount - messagesReceivedCount;

            //retrieve  messages
            var messages = _consumer.Receive(TimeSpan.FromMilliseconds(10000));

            Assert.True(messages.Length <= outstandingMessageCount);

            //should not receive more than buffer in one hit
            Assert.True(messages.Length <= BufferSize);

            var moreMessages = messages.Where(m => m.Header.MessageType == MessageType.MT_COMMAND);
            foreach (var message in moreMessages)
            {
                messagesReceived.Add(message);
                _consumer.Acknowledge(message);
            }

            messagesReceivedCount = messagesReceived.Count;

            await Task.Delay(1000);
        } while ((iteration <= 5) && (messagesReceivedCount < MessageCount));


        Assert.Equal(4, messagesReceivedCount);
    }

    public void Dispose()
    {
        //Clean up resources that we have created
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        _messageProducer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await ((IAmAMessageProducerAsync)_messageProducer).DisposeAsync();
    }
}
