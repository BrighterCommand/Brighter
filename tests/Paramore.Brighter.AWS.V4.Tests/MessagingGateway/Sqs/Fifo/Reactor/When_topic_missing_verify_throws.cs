using System;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Fifo.Reactor;

[Trait("Category", "AWS")]
public class AWSValidateMissingTopicTests
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly RoutingKey _routingKey;

    public AWSValidateMissingTopicTests()
    {
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _routingKey = new RoutingKey(queueName);

        _awsConnection = GatewayFactory.CreateFactory();

        // Because we don't use channel factory to create the infrastructure - it won't exist
    }

    [Fact]
    public void When_channel_missing_verify_throws()
    {
        // arrange
        var producer = new SqsMessageProducer(
            _awsConnection,
            new SqsPublication(
                channelName: new ChannelName(Guid.NewGuid().ToString()), 
                queueAttributes: new SqsAttributes (type:SqsType.Fifo ),
                makeChannels: OnMissingChannel.Validate
                )
            );

        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";

        // act & assert
        Assert.Throws<QueueDoesNotExistException>(() =>
            producer.Send(new Message(
                new MessageHeader("", _routingKey, MessageType.MT_EVENT,
                    type: new CloudEventsType("plain/text"), partitionKey: messageGroupId),
                new MessageBody("Test"))));
    }
}
