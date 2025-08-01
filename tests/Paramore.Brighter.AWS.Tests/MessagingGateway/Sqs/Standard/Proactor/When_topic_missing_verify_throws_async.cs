using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Proactor;

[Trait("Category", "AWS")]
public class AWSValidateMissingTopicTestsAsync 
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly RoutingKey _routingKey;
    private readonly string _queueName;

    public AWSValidateMissingTopicTestsAsync()
    {
         _queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _routingKey = new RoutingKey(_queueName);

        _awsConnection = GatewayFactory.CreateFactory();

        // Because we don't use channel factory to create the infrastructure - it won't exist
    }

    [Fact]
    public async Task When_topic_missing_verify_throws_async()
    {
        // arrange
        var producer = new SqsMessageProducer(
            _awsConnection,
            new SqsPublication(channelName: new ChannelName(_queueName), makeChannels: OnMissingChannel.Validate)
            );

        // act & assert
        await Assert.ThrowsAsync<QueueDoesNotExistException>(async () => 
            await producer.SendAsync(new Message(
                new MessageHeader("", _routingKey, MessageType.MT_EVENT, type: new CloudEventsType("plain/text")),
                new MessageBody("Test"))));
    }
}
