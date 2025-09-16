using System;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Fifo.Proactor;

[Trait("Category", "AWS")]
public class AwsValidateMissingTopicTestsAsync
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly RoutingKey _routingKey;

    public AwsValidateMissingTopicTestsAsync()
    {
        string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _routingKey = new RoutingKey(topicName);

        _awsConnection = GatewayFactory.CreateFactory();

        // Because we don't use channel factory to create the infrastructure - it won't exist
    }

    [Fact]
    public async Task When_topic_missing_verify_throws_async()
    {
        // arrange
        var producer = new SnsMessageProducer(
            _awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Validate,
                TopicAttributes = new SnsAttributes { Type = SqsType.Fifo }
            });

        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";

        // act & assert
        await Assert.ThrowsAsync<BrokerUnreachableException>(async () =>
            await producer.SendAsync(new Message(
                new MessageHeader("", _routingKey, MessageType.MT_EVENT,
                    type: new CloudEventsType("plain/text"), partitionKey: messageGroupId),
                new MessageBody("Test"))));
    }
}
