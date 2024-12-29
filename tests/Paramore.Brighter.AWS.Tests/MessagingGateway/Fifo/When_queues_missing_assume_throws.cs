using System;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Fifo;

[Trait("Category", "AWS")] 
public class AWSAssumeQueuesTests  : IDisposable
{
    private readonly ChannelFactory _channelFactory;
    private readonly SqsMessageConsumer _consumer;

    public AWSAssumeQueuesTests()
    {
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);
            
        var subscription = new SqsSubscription<MyCommand>(
            name: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            makeChannels: OnMissingChannel.Assume,
            sqsType: SnsSqsType.Fifo
        );
            
        (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
        var awsConnection = new AWSMessagingGatewayConnection(credentials, region);
            
        //create the topic, we want the queue to be the issue
        //We need to create the topic at least, to check the queues
        var producer = new SqsMessageProducer(awsConnection, 
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create,
                SnsType = SnsSqsType.Fifo
            });
            
        producer.ConfirmTopicExistsAsync(topicName).Wait(); 
            
        _channelFactory = new ChannelFactory(awsConnection);
        var channel = _channelFactory.CreateChannel(subscription);
            
        //We need to create the topic at least, to check the queues
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), routingKey);
    }

    [Fact]
    public void When_queues_missing_assume_throws()
    {
        //we will try to get the queue url, and fail because it does not exist
        Assert.Throws<QueueDoesNotExistException>(() => _consumer.Receive(TimeSpan.FromMilliseconds(1000)));
    }
 
    public void Dispose()
    {
        _channelFactory.DeleteTopic(); 
    }
}
