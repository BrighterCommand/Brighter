using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Fifo.Proactor;

[Trait("Category", "AWS")]
public class AwsValidateMissingTopicTestsAsync
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly RoutingKey _routingKey;
    private readonly ChannelName _channelName;

    public AwsValidateMissingTopicTestsAsync()
    { 
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _channelName = new ChannelName(queueName);
        _routingKey = new RoutingKey(_channelName);
        _awsConnection = GatewayFactory.CreateFactory();

        // Because we don't use channel factory to create the infrastructure - it won't exist
    }

    [Fact]
    public async Task When_queue_missing_verify_throws_async()
    {
        // arrange
        var producer = new SqsMessageProducer(_awsConnection,
            new SqsPublication
            (
                channelName: new ChannelName(_channelName!),
                queueAttributes: new SqsAttributes(type: SqsType.Fifo),
                makeChannels: OnMissingChannel.Validate
            ));

        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";

        // act & assert
        await Assert.ThrowsAsync<QueueDoesNotExistException>(async () =>
            await producer.SendAsync(new Message(
                new MessageHeader("", _routingKey, MessageType.MT_EVENT,
                    type: new CloudEventsType("plain/text"), partitionKey: messageGroupId),
                new MessageBody("Test"))));
    }
}
