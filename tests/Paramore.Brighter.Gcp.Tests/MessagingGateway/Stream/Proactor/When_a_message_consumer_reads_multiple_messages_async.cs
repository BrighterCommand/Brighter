using System;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream.Proactor;

[Trait("Category", "GCP")]
public class StreamPubSubBufferedConsumerTestsAsync : IDisposable
{
    private readonly ContentType _contentType = new("text/plain");
    private readonly GcpMessageProducer _messageProducer;
    private readonly GcpSubscription _subscription;
    private readonly IAmAChannelAsync _channel;
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

        _channel = _channelFactory.CreateAsyncChannelAsync(_subscription = new GcpSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            bufferSize: BufferSize,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream
        )).GetAwaiter().GetResult();
        
        _messageProducer = new GcpMessageProducer(gcpConnection,
            new GcpPublication { MakeChannels = OnMissingChannel.Create, Topic = routingKey });
    }

    [Fact]
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
            await Task.Delay(TimeSpan.FromSeconds(1));
            
            //retrieve  messages
            var messages = await _channel.ReceiveAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(MessageType.MT_COMMAND, messages.Header.MessageType);
            
            await _channel.AcknowledgeAsync(messages);
        }
    }

    public void Dispose()
    {
        _channel.Dispose();
        Task.Delay(TimeSpan.FromMilliseconds(100)).GetAwaiter().GetResult();
        
       _channelFactory.DeleteSubscription(_subscription);
       _channelFactory.DeleteTopic(_subscription);
       _messageProducer.Dispose();
    }
}
