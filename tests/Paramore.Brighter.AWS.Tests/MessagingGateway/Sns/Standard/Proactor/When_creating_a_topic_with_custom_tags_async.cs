using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Standard.Proactor;

[Trait("Category", "AWS")]
public class SqsMessageProducerCreateTopicWithTagsAsyncTests : IAsyncDisposable, IDisposable
{
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly string _topicName;
    private readonly Message _message;

    public SqsMessageProducerCreateTopicWithTagsAsyncTests()
    {
        var myCommand = new MyCommand { Value = "Test" };
        var channelName = $"Producer-Tag-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _topicName = $"Producer-Tag-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(_topicName);

        var topicAttributes = new SnsAttributes(
            tags: [new Tag { Key = "Environment", Value = "Test" }]
        );

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            queueAttributes: new SqsAttributes(rawMessageDelivery: false, tags: new Dictionary<string, string> { { "Environment", "Test" } }),
            topicAttributes: topicAttributes
        );

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        _awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(_awsConnection);
        _channelFactory.CreateAsyncChannel(subscription);

        _messageProducer = new SnsMessageProducer(
            _awsConnection,
            new SnsPublication
            {
                Topic = new RoutingKey(_topicName),
                MakeChannels = OnMissingChannel.Create,
                TopicAttributes = topicAttributes
            });
    }

    [Fact]
    public async Task When_creating_a_topic_with_custom_tags_async()
    {
        //arrange
        await _messageProducer.SendAsync(_message);

        //act
        using var snsClient = new AWSClientFactory(_awsConnection).CreateSnsClient();
        var topicArn = (await snsClient.FindTopicAsync(_topicName)).TopicArn;
        var tagsResponse = await snsClient.ListTagsForResourceAsync(
            new ListTagsForResourceRequest { ResourceArn = topicArn });

        //assert
        Assert.Contains(tagsResponse.Tags, t => t.Key == "Source" && t.Value == "Brighter");
        Assert.Contains(tagsResponse.Tags, t => t.Key == "Environment" && t.Value == "Test");
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        _messageProducer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _messageProducer.DisposeAsync();
    }
}
