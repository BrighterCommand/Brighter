using System;
using System.Net.Mime;
using System.Threading;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream.Reactor;

[Trait("Category", "GCP")]
public class StreamPubSubBufferedConsumerTestsAsync : IDisposable
{
    private readonly ContentType _contentType = new("text/plain");
    private readonly GcpMessageProducer _messageProducer;
    private readonly GcpSubscription _subscription;
    private readonly IAmAChannelSync _channel;
    private readonly string _topicName;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private const int BufferSize = 3;
    private const int MessageCount = 4;

    public StreamPubSubBufferedConsumerTestsAsync()
    {
        var gcpConnection = GatewayFactory.CreateFactory();

        _channelFactory = new GcpPubSubChannelFactory(gcpConnection);
        var channelName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _topicName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var routingKey = new RoutingKey(_topicName);

        _channel = _channelFactory.CreateSyncChannel(_subscription = new GcpSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            bufferSize: BufferSize,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull
        ));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
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

        for(var i = 0; i < MessageCount; i++)
        {
            Thread.Sleep(TimeSpan.FromSeconds(1));
            
            //retrieve  messages
            var messages = _channel.Receive(TimeSpan.FromSeconds(10));

            Assert.Equal(MessageType.MT_COMMAND, messages.Header.MessageType);
            _channel.Acknowledge(messages);
        } 
    }

    public void Dispose()
    {
        _channelFactory.DeleteSubscription(_subscription);
        _channelFactory.DeleteTopic(_subscription);
        _messageProducer.DisposeAsync().GetAwaiter().GetResult();
    }
}
