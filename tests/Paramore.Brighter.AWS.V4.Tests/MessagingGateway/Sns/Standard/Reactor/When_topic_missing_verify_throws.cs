using System;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Standard.Reactor;

[Trait("Category", "AWS")]
public class AwsValidateMissingTopicTests
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly RoutingKey _routingKey;

    public AwsValidateMissingTopicTests()
    {
        string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _routingKey = new RoutingKey(topicName);

        _awsConnection = GatewayFactory.CreateFactory();

        //Because we don't use channel factory to create the infrastructure -it won't exist
    }

    [Fact]
    public void When_topic_missing_verify_throws()
    {
        //arrange
        var producer = new SnsMessageProducer(_awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Validate,
            });

        //act && assert
        Assert.Throws<BrokerUnreachableException>(() => producer.Send(new Message(
            new MessageHeader("", _routingKey, MessageType.MT_EVENT, type: new CloudEventsType("plain/text")),
            new MessageBody("Test"))));
    }
}
