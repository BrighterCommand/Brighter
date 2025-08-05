using System;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Standard.Proactor;

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
        var producer = new SnsMessageProducer(_awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Validate
            });

        // act & assert
        await Assert.ThrowsAsync<BrokerUnreachableException>(async () => 
            await producer.SendAsync(new Message(
                new MessageHeader("", _routingKey, MessageType.MT_EVENT, type: new CloudEventsType("plain/text")),
                new MessageBody("Test"))));
    }
}
