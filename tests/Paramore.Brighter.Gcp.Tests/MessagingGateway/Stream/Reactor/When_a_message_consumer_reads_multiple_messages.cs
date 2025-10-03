using System;
using System.Net.Mime;
using System.Threading;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream.Reactor;

[Trait("Category", "GCP")]
public class PubSubBufferedConsumerTestsAsync : IDisposable
{
    private readonly ContentType _contentType = new("text/plain");
    private readonly GcpMessageProducer _messageProducer;
    private readonly GcpPubSubSubscription _pubSubSubscription;
    private readonly IAmAChannelSync _channel;
    private readonly string _topicName;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private const int BufferSize = 3;
    private const int MessageCount = 4;

    public PubSubBufferedConsumerTestsAsync()
    {
         _channelFactory = GatewayFactory.CreateChannelFactory();
        var channelName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _topicName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var routingKey = new RoutingKey(_topicName);

        _channel = _channelFactory.CreateSyncChannel(_pubSubSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            bufferSize: BufferSize,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream
        ));

        _messageProducer = GatewayFactory.CreateProducer(new GcpPublication<MyCommand>
        {
            MakeChannels = OnMissingChannel.Create,
            Topic = routingKey
        });
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
            //retrieve  messages
            var message = _channel.Receive(TimeSpan.FromMilliseconds(10000));
            Assert.NotEqual(MessageType.MT_NONE, message.Header.MessageType);

            _channel.Acknowledge(message);
            Thread.Sleep(1000);
        } 
    }

    public void Dispose()
    {
        _channelFactory.DeleteSubscription(_pubSubSubscription);
        _channelFactory.DeleteTopic(_pubSubSubscription);
        _messageProducer.DisposeAsync().GetAwaiter().GetResult();
    }
}
