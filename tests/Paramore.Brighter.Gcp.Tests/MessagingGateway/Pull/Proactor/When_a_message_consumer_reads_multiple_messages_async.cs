using System;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull.Proactor;

[Category("GCP")]
public class PubSubBufferedConsumerTestsAsync : IDisposable
{
    private readonly ContentType _contentType = new("text/plain");
    private GcpMessageProducer _messageProducer;
    private GcpPubSubSubscription _pubSubSubscription;
    private IAmAChannelAsync _channel;
    private readonly string _topicName;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private const int BufferSize = 3;
    private const int MessageCount = 4;

    public PubSubBufferedConsumerTestsAsync()
    {
        _channelFactory = GatewayFactory.CreateChannelFactory();
        _topicName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
    }

    [Before(Test)]
    public async Task Setup()
    {
        var channelName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var routingKey = new RoutingKey(_topicName);

        _channel = await _channelFactory.CreateAsyncChannelAsync(_pubSubSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            bufferSize: BufferSize,
            messagePumpType: MessagePumpType.Proactor,
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
    public async Task When_a_message_consumer_reads_multiple_messages_async()
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
            var message = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(10000));
            await Assert.That(message.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);

            await _channel.AcknowledgeAsync(message);
            await Task.Delay(1000);
        } 
    }

    public void Dispose()
    {
       _channelFactory.DeleteSubscription(_pubSubSubscription);
       _channelFactory.DeleteTopic(_pubSubSubscription);
       _messageProducer.Dispose();
    }
}
