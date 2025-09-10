using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Runtime.CredentialManagement;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Standard.Proactor;

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
        string correlationId = Guid.NewGuid().ToString();
        string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = $"Producer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        string topicName = $"Producer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType:  ChannelType.PubSub,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create
        );

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        new CredentialProfileStoreChain();

        var awsConnection = GatewayFactory.CreateFactory();

        _sender = new SnsMessageProducer(awsConnection, new SnsPublication { MakeChannels = OnMissingChannel.Create });

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
