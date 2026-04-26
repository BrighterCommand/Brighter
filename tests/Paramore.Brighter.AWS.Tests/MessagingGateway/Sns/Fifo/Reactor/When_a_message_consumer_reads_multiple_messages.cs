using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Fifo.Reactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class SqsBufferedConsumerTests : IAsyncDisposable
{
    private readonly SnsMessageProducer _messageProducer;
    private readonly SqsMessageConsumer _consumer;
    private readonly string _topicName;
    private readonly ChannelFactory _channelFactory;
    private readonly ContentType _contentType = new(MediaTypeNames.Text.Plain);
    private const int BufferSize = 3;
    private const int MessageCount = 4;

    public SqsBufferedConsumerTests()
    {
        var awsConnection = GatewayFactory.CreateFactory();
        _channelFactory = new ChannelFactory(awsConnection);

        var channelName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _topicName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var routingKey = new RoutingKey(_topicName);
        var topicAttributes = new SnsAttributes { Type = SqsType.Fifo };

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            bufferSize: BufferSize,
            queueAttributes: new SqsAttributes(
                type: SqsType.Fifo,
                contentBasedDeduplication: true,
                deduplicationScope: DeduplicationScope.MessageGroup,
                fifoThroughputLimit: FifoThroughputLimit.PerMessageGroupId,
                tags: new Dictionary<string, string> { { "Environment", "Test" } }), 
            topicAttributes: topicAttributes,
            makeChannels: OnMissingChannel.Create,
            messagePumpType: MessagePumpType.Reactor);
        
        var channel = _channelFactory.CreateSyncChannel(subscription);

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(true), BufferSize);
        _messageProducer = new SnsMessageProducer(awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create,
                TopicAttributes = topicAttributes
            });
    }

    [Test]
    public async Task When_a_message_consumer_reads_multiple_messages()
    {
        var routingKey = new RoutingKey(_topicName);

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
        await _messageProducer.SendAsync(messageOne);
        await _messageProducer.SendAsync(messageTwo);
        await _messageProducer.SendAsync(messageThree);
        await _messageProducer.SendAsync(messageFour);
        await _messageProducer.SendAsync(messageFive);


        int iteration = 0;
        var messagesReceived = new List<Message>();
        var messagesReceivedCount = messagesReceived.Count;
        do
        {
            iteration++;
            var outstandingMessageCount = MessageCount - messagesReceivedCount;

            //retrieve  messages
            var messages = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000));

            await Assert.That(messages.Length <= outstandingMessageCount).IsTrue();

            //should not receive more than buffer in one hit
            await Assert.That(messages.Length <= BufferSize).IsTrue();

            var moreMessages = messages.Where(m => m.Header.MessageType == MessageType.MT_COMMAND);
            foreach (var message in moreMessages)
            {
                messagesReceived.Add(message);
                await _consumer.AcknowledgeAsync(message);
            }

            messagesReceivedCount = messagesReceived.Count;

            await Task.Delay(1000);
        } while ((iteration <= 5) && (messagesReceivedCount < MessageCount));


        await Assert.That(messagesReceivedCount).IsEqualTo(4);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        //Clean up resources that we have created
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _messageProducer.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await ((IAmAMessageProducerAsync)_messageProducer).DisposeAsync();
    }
}
