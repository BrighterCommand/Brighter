using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Standard.Proactor;

[Trait("Category", "AWS")]
public class SqsMessageProducerResourcesAreTaggedAsyncTests : IAsyncDisposable, IDisposable
{
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly string _topicName;
    private readonly string _channelName;
    private readonly Message _message;

    public SqsMessageProducerResourcesAreTaggedAsyncTests()
    {
        var myCommand = new MyCommand { Value = "Test" };
        _channelName = $"Producer-Tag-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _topicName = $"Producer-Tag-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(_topicName);

        var topicAttributes = new SnsAttributes(
            tags: [new Tag { Key = "Environment", Value = "Test" }]
        );

        var queueAttributes = new SqsAttributes(
            rawMessageDelivery: false,
            tags: new Dictionary<string, string> { { "Environment", "Test" } }
        );

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(_channelName),
            channelName: new ChannelName(_channelName),
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            queueAttributes: queueAttributes,
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
    public async Task When_posting_a_message_resources_are_tagged_async()
    {
        //arrange
        await _messageProducer.SendAsync(_message);

        //act - verify topic tags
        using var snsClient = new AWSClientFactory(_awsConnection).CreateSnsClient();
        var topicArn = (await snsClient.FindTopicAsync(_topicName)).TopicArn;
        var topicTagsResponse = await snsClient.ListTagsForResourceAsync(
            new ListTagsForResourceRequest { ResourceArn = topicArn });

        //act - verify queue tags
        using var sqsClient = new AWSClientFactory(_awsConnection).CreateSqsClient();
        var queueUrlResponse = await sqsClient.GetQueueUrlAsync(_channelName);
        var queueTagsResponse = await sqsClient.ListQueueTagsAsync(
            new ListQueueTagsRequest { QueueUrl = queueUrlResponse.QueueUrl });

        //assert - topic has Environment=Test tag
        Assert.Contains(topicTagsResponse.Tags, t => t.Key == "Environment" && t.Value == "Test");

        //assert - queue has Environment=Test tag
        Assert.True(queueTagsResponse.Tags.ContainsKey("Environment"));
        Assert.Equal("Test", queueTagsResponse.Tags["Environment"]);
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
