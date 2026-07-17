using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Standard.Reactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class SqsMessageConsumerRejectTests
{
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public SqsMessageConsumerRejectTests()
    {
        _myCommand = new MyCommand{Value = "Test"};
        string correlationId = Guid.NewGuid().ToString();
        string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        string topicName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        SqsSubscription<MyCommand> subscription = new(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }),
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]),
            makeChannels: OnMissingChannel.Create);

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object) _myCommand, JsonSerialisationOptions.Options))
        );

        //Must have credentials stored in the SDK Credentials store or shared credentials file
        var awsConnection = GatewayFactory.CreateFactory();

        //We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = new ChannelFactory(awsConnection);
        _channel = _channelFactory.CreateSyncChannel(subscription);

        _messageProducer = new SnsMessageProducer(awsConnection, new SnsPublication{MakeChannels = OnMissingChannel.Create});
    }

    [Test]
    public async Task When_rejecting_a_message_should_delete_from_queue()
    {
        //Arrange
        await _messageProducer.SendAsync(_message);
        var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

        //Act
        _channel.Reject(message);

        //Assert - message should be deleted, not requeued
        message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
