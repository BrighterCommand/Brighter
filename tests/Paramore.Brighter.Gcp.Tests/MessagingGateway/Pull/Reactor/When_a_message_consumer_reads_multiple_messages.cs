using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull.Reactor;

[Trait("Category", "GCP")]
public class PubSubBufferedConsumerTestsAsync : IDisposable
{
    private readonly ContentType _contentType = new("text/plain");
    private readonly GcpMessageProducer _messageProducer;
    private readonly GcpSubscription _subscription;
    private readonly GcpPullMessageConsumer _consumer;
    private readonly string _topicName;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private const int BufferSize = 3;
    private const int MessageCount = 4;

    public PubSubBufferedConsumerTestsAsync()
    {
        var gcpConnection = GatewayFactory.CreateFactory();

        _channelFactory = new GcpPubSubChannelFactory(gcpConnection);
        var channelName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _topicName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var routingKey = new RoutingKey(_topicName);

        var channel = _channelFactory.CreateAsyncChannelAsync(_subscription = new GcpSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            bufferSize: BufferSize,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull
        )).GetAwaiter().GetResult();

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new GcpPullMessageConsumer(gcpConnection, 
            new Google.Cloud.PubSub.V1.SubscriptionName(gcpConnection.ProjectId, channel.Name), 
            BufferSize, false, TimeProvider.System);
        _messageProducer = new GcpMessageProducer(gcpConnection,
            new GcpPublication { MakeChannels = OnMissingChannel.Create, Topic = routingKey });
    }

    [Fact]
    public  void When_a_message_consumer_reads_multiple_messages()
    {
        var routingKey = new RoutingKey(_topicName);

        var messageOne = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content one")
        );

        var messageTwo = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content two")
        );

        var messageThree = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content three")
        );

        var messageFour = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content four")
        );

        //send MESSAGE_COUNT messages
        _messageProducer.Send(messageOne);
        _messageProducer.Send(messageTwo);
        _messageProducer.Send(messageThree);
        _messageProducer.Send(messageFour);

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

            Task.Delay(1000).GetAwaiter().GetResult();

        } while ((iteration <= 5) && (messagesReceivedCount < MessageCount));

        Assert.Equal(4, messagesReceivedCount);
    }

    public void Dispose()
    {
        _channelFactory.DeleteSubscription(_subscription);
        _channelFactory.DeleteTopic(_subscription);
        _messageProducer.DisposeAsync().GetAwaiter().GetResult();
    }
}
