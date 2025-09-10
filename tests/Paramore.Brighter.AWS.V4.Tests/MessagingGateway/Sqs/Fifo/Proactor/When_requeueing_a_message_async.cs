using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Fifo.Proactor;

[Trait("Category", "AWS")]
public class SqsMessageProducerRequeueTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _sender;
    private Message? _requeuedMessage;
    private Message? _receivedMessage;
    private readonly IAmAChannelAsync _channel;
    private readonly ChannelFactory _channelFactory;
    private readonly Message _message;

    public SqsMessageProducerRequeueTestsAsync()
    {
        MyCommand myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var correlationId = Guid.NewGuid().ToString();
        var subcriptionName = $"Producer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var routingKey = new RoutingKey(queueName);

        var queueAttributes = new SqsAttributes(
            type: SqsType.Fifo);
        var channelName = new ChannelName(queueName);
        
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subcriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            queueAttributes: queueAttributes, 
            makeChannels: OnMissingChannel.Create);

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType, partitionKey: messageGroupId),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        _sender = new SqsMessageProducer(awsConnection,
            new SqsPublication(channelName: channelName, makeChannels: OnMissingChannel.Create, queueAttributes: queueAttributes));

        _channelFactory = new ChannelFactory(awsConnection);
        _channel = _channelFactory.CreateAsyncChannel(subscription);
    }

    [Fact]
    public async Task When_requeueing_a_message_async()
    {
        await _sender.SendAsync(_message);
        _receivedMessage = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));
        await _channel.RequeueAsync(_receivedMessage);

        _requeuedMessage = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));
        
        await _channel.AcknowledgeAsync(_requeuedMessage);

        Assert.Equal(_receivedMessage.Body.Value, _requeuedMessage.Body.Value);
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
