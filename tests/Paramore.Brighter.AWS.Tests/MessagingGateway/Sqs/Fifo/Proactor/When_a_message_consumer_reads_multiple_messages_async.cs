using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Fifo.Proactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class SqsBufferedConsumerTestsAsync : IAsyncDisposable
{
    private SqsMessageProducer _messageProducer;
    private SqsMessageConsumer _consumer;
    private string _queueName;
    private ChannelFactory _channelFactory;
    private readonly ContentType _contentType = new(MediaTypeNames.Text.Plain);
    private const int BufferSize = 3;
    private const int MessageCount = 4;

    [Before(Test)]
    public async Task Setup()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        _queueName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var channelName = new ChannelName(_queueName);
        var routingKey = new RoutingKey(_queueName);
        var queueAttributes = new SqsAttributes(
            type: SqsType.Fifo,
            messageRetentionPeriod: TimeSpan.FromSeconds(3600),
            deduplicationScope: DeduplicationScope.MessageGroup,
            fifoThroughputLimit: FifoThroughputLimit.PerMessageGroupId,
            tags: new Dictionary<string, string> { { "Environment", "Test" } });

        var channel = await _channelFactory.CreateAsyncChannelAsync(new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(_queueName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            bufferSize: BufferSize,
            queueAttributes: queueAttributes,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(true), BufferSize);
        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication(channelName: channelName, queueAttributes: queueAttributes, makeChannels: OnMissingChannel.Create));
    }

    [Test]
    public async Task When_a_message_consumer_reads_multiple_messages_async()
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

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _messageProducer.DisposeAsync();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _messageProducer.DisposeAsync();
    }
}
