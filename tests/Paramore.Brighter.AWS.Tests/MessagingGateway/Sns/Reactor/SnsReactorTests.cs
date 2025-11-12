using System;
using System.Linq;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.Base.Test.MessagingGateway.Reactor;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;
using Xunit.Sdk;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Reactor;

public partial class SnsReactorTests : MessagingGatewayReactorTests<SnsPublication, SqsSubscription>
{
    protected override bool HasSupportToDeadLetterQueue => true;
    protected override bool HasSupportToMoveToDeadLetterQueueAfterTooManyRetries => true;

    protected virtual SqsType TopicType { get; } = SqsType.Standard;
    protected virtual bool ContentBasedDeduplication { get; } = true;

    protected virtual bool RawMessageDelivery { get; } = true;

    protected override SnsPublication CreatePublication(RoutingKey routingKey)
    {
        return new SnsPublication<MyCommand>
        {
            Topic = routingKey,
            TopicAttributes = new SnsAttributes(
                type: TopicType, 
                contentBasedDeduplication: ContentBasedDeduplication),
            MakeChannels = OnMissingChannel.Create,
        };
    }

    protected override SqsSubscription CreateSubscription(RoutingKey routingKey, ChannelName channelName,
        OnMissingChannel makeChannel = OnMissingChannel.Create, bool setupDeadLetterQueue = false)
    {
        var snsAttributes = new SnsAttributes(
            type: TopicType,
            contentBasedDeduplication: ContentBasedDeduplication);
        var sqsAttributes = new SqsAttributes(
            type: TopicType,
            contentBasedDeduplication: ContentBasedDeduplication,
            rawMessageDelivery: RawMessageDelivery);
        
        if (setupDeadLetterQueue)
        {
            sqsAttributes = new SqsAttributes(
                type: TopicType,
                contentBasedDeduplication: ContentBasedDeduplication,
                redrivePolicy: new RedrivePolicy($"{channelName.Value}DLQ", 3));
        }
        
        return new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            routingKey: routingKey,
            channelName: channelName,
            requeueCount: 3,
            queueAttributes: sqsAttributes,
            topicAttributes: snsAttributes,
            makeChannels: makeChannel
        );
    }

    protected override IAmAMessageProducerSync CreateProducer(SnsPublication publication)
    {
        var producers = new SnsMessageProducerFactory(GatewayFactory.CreateFactory(), [publication])
            .Create();
        
        var producer = producers.First().Value;
        return (IAmAMessageProducerSync)producer;
    }

    protected override IAmAChannelSync CreateChannel(SqsSubscription subscription)
    {
        var channel = new ChannelFactory(GatewayFactory.CreateFactory())
            .CreateSyncChannel(subscription);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            channel.Receive(TimeSpan.FromMilliseconds(100));
        }

        return channel;
    }

    protected override Message GetMessageFromDeadLetterQueue(SqsSubscription subscription)
    {
        var sqsAttributes = new SqsAttributes(
            type: TopicType,
            contentBasedDeduplication: ContentBasedDeduplication,
            rawMessageDelivery: true);
        
        var sub =  new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            routingKey: $"{subscription.ChannelName.Value}DLQ",
            channelName: $"{subscription.ChannelName.Value}DLQ",
            channelType: ChannelType.PointToPoint,
            makeChannels: OnMissingChannel.Assume,
            queueAttributes: sqsAttributes
        );
        
        using var channel = new ChannelFactory(GatewayFactory.CreateFactory())
            .CreateSyncChannel(sub);
        return channel.Receive(ReceiveTimeout);
    }
    
    protected override void CleanUp()
    {
        base.CleanUp();
        var factory = new AWSClientFactory(GatewayFactory.CreateFactory());
        
        if (Subscription != null)
        {
            var sqs = factory.CreateSqsClient();
            DeleteQueue(sqs, $"{Subscription.ChannelName.Value}DLQ");
            DeleteQueue(sqs, Subscription.ChannelName.Value);

            var sns = factory.CreateSnsClient();
            DeleteTopic(sns, Subscription.RoutingKey.Value);
            
        }
        
        if (Publication != null)
        {
            
            var sns = factory.CreateSnsClient();
            DeleteTopic(sns, Publication.Topic!.Value);
        }

        return;

        void DeleteQueue(AmazonSQSClient client, string queueName)
        {
            try
            {
                var queueUrl = client.GetQueueUrlAsync($"{queueName.ToValidSQSQueueName(TopicType == SqsType.Fifo)}")
                    .GetAwaiter()
                    .GetResult();
                
                client.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = queueUrl.QueueUrl })
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception)
            {
                // Ignoring any error
            }
        }
        
        void DeleteTopic(AmazonSimpleNotificationServiceClient client, string topicName)
        {
            try
            {
                var topic = client.FindTopicAsync(topicName).GetAwaiter().GetResult();
                client.DeleteTopicAsync(new DeleteTopicRequest { TopicArn = topic.TopicArn })
                .GetAwaiter()
                .GetResult();
            }
            catch (Exception)
            {
                // Ignoring any error
            }
        }
    }

    [Fact]
    public void When_topic_missing_verify_throws()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Publication.MakeChannels = OnMissingChannel.Validate;
        
        
        // act & assert
        try
        {
            Producer = CreateProducer(Publication);
            Assert.Fail("Expecting an exception");
        }
        catch (Exception e) when(e is not XunitException)
        {
            Assert.IsType<BrokerUnreachableException>(e);
        }
    }
    
    [Fact]
    public void When_queues_missing_verify_throws()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!,
            GetOrCreateChannelName(),
            OnMissingChannel.Validate);
        
        // act & assert
        try
        {
            // Ensure the topic is created
            Producer = CreateProducer(Publication);
            Channel = CreateChannel(Subscription);
            Assert.Fail("Expecting an exception");
        }
        catch (Exception e) when(e is not XunitException)
        {
            Assert.IsType<QueueDoesNotExistException>(e);
        }
    }
    
    [Fact]
    public void When_queues_missing_assume_throws()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!,
            GetOrCreateChannelName(),
            OnMissingChannel.Assume);
        
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);
        
        
        // act & assert
        try
        {
            // Ensure the topic is created
            Producer.Send(CreateMessage(Publication.Topic!));
            Channel.Receive(ReceiveTimeout);
            Assert.Fail("Expecting an exception");
        }
        catch (Exception e) when(e is not XunitException)
        {
            Assert.IsType<QueueDoesNotExistException>(e);
        }
    }
}
