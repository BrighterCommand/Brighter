using System;
using System.Net.Mime;
using System.Threading;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull.Reactor;

[Category("GCP")]
public class PubSubBufferedConsumerTestsAsync
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
            subscriptionMode: SubscriptionMode.Pull
        ));

        _messageProducer = GatewayFactory.CreateProducer(new GcpPublication<MyCommand>
        {
            MakeChannels = OnMissingChannel.Create,
            Topic = routingKey
        });
    }

    [Test]
    public async Task When_a_message_consumer_reads_multiple_messages()
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
        await _messageProducer.SendAsync(messageOne);
        await _messageProducer.SendAsync(messageTwo);
        await _messageProducer.SendAsync(messageThree);
        await _messageProducer.SendAsync(messageFour);

        for(var i = 0; i < MessageCount; i++)
        {
            //retrieve  messages
            var message = _channel.Receive(TimeSpan.FromMilliseconds(10000));
            await Assert.That(message.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            _channel.Acknowledge(message);
            Thread.Sleep(1000);
        } 
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteSubscriptionAsync(_pubSubSubscription);
        await _channelFactory.DeleteTopicAsync(_pubSubSubscription);
        await _messageProducer.DisposeAsync();
    }
}
