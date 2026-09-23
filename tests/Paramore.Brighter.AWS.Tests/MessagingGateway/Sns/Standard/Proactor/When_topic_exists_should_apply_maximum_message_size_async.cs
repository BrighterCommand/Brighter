using System;
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
public class SnsMessageProducerExistingTopicMaximumMessageSizeAsyncTests : IAsyncDisposable, IDisposable
{
    private const int OneMebibyte = 1_048_576;

    private readonly SnsMessageProducer _messageProducer;
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly string _topicName;
    private readonly Message _message;

    public SnsMessageProducerExistingTopicMaximumMessageSizeAsyncTests()
    {
        var myCommand = new MyCommand { Value = "Test" };
        _topicName = $"Producer-Size-Tests-{Guid.NewGuid()}".Truncate(45);
        var routingKey = new RoutingKey(_topicName);

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        _awsConnection = GatewayFactory.CreateFactory();

        _messageProducer = new SnsMessageProducer(
            _awsConnection,
            new SnsPublication
            {
                Topic = routingKey,
                MakeChannels = OnMissingChannel.Create,
                TopicAttributes = new SnsAttributes(maximumMessageSize: OneMebibyte)
            });
    }

    [Fact]
    public async Task When_topic_exists_should_apply_maximum_message_size_async()
    {
        //arrange - a topic Brighter created earlier already exists at the SNS default of 256 KiB
        using var snsClient = new AWSClientFactory(_awsConnection).CreateSnsClient();
        var existingTopic = await snsClient.CreateTopicAsync(new CreateTopicRequest(_topicName)
        {
            Tags = [new Tag { Key = "Source", Value = "Brighter" }]
        });

        //act
        await _messageProducer.SendAsync(_message);

        //assert
        var topicAttributes = await snsClient.GetTopicAttributesAsync(new GetTopicAttributesRequest(existingTopic.TopicArn));
        Assert.Equal("1048576", topicAttributes.Attributes["MaximumMessageSize"]);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        using var snsClient = new AWSClientFactory(_awsConnection).CreateSnsClient();
        var topic = await snsClient.FindTopicAsync(_topicName);
        if (topic is not null)
            await snsClient.DeleteTopicAsync(topic.TopicArn);

        await _messageProducer.DisposeAsync();
    }
}
