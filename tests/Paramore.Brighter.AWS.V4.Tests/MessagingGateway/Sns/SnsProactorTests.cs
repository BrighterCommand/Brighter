using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;
using Xunit.Sdk;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns;

[Collection("MessagingGateway")]
public class SnsProactorTests : MessagingGatewayProactorTests<SnsPublication, SqsSubscription>
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

    protected override async Task<IAmAMessageProducerAsync> CreateProducerAsync(SnsPublication publication, CancellationToken cancellationToken = default)
    {
        var producers = await new SnsMessageProducerFactory(GatewayFactory.CreateFactory(), [publication])
            .CreateAsync();
        
        var producer = producers.First().Value;
        return (IAmAMessageProducerAsync)producer;
    }

    protected override async Task<IAmAChannelAsync> CreateChannelAsync(SqsSubscription subscription, CancellationToken cancellationToken = default)
    {
        var channel = await new ChannelFactory(GatewayFactory.CreateFactory())
            .CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            await channel.ReceiveAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        return channel;
    }

    protected override async Task<Message> GetMessageFromDeadLetterQueueAsync(SqsSubscription subscription, CancellationToken cancellationToken = default)
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
        
        using var channel = await new ChannelFactory(GatewayFactory.CreateFactory())
            .CreateAsyncChannelAsync(sub, cancellationToken);
        return await channel.ReceiveAsync(ReceiveTimeout, cancellationToken);
    }
    
    protected override async Task CleanUpAsync(CancellationToken cancellationToken = default)
    {
        await base.CleanUpAsync(cancellationToken);
        var factory = new AWSClientFactory(GatewayFactory.CreateFactory());
        
        if (Subscription != null)
        {
            var sqs = factory.CreateSqsClient();
            await DeleteQueueAsync(sqs, $"{Subscription.ChannelName.Value}DLQ");
            await DeleteQueueAsync(sqs, Subscription.ChannelName.Value);

            var sns = factory.CreateSnsClient();
            await DeleteTopicAsync(sns, Subscription.RoutingKey.Value);
            
        }
        
        if (Publication != null)
        {
            
            var sns = factory.CreateSnsClient();
            await DeleteTopicAsync(sns, Publication.Topic!.Value);
        }

        return;

        async Task DeleteQueueAsync(AmazonSQSClient client, string queueName)
        {
            try
            {
                var queueUrl = await client.GetQueueUrlAsync($"{queueName.ToValidSQSQueueName(TopicType == SqsType.Fifo)}", cancellationToken);
                await client.DeleteQueueAsync(new DeleteQueueRequest { QueueUrl = queueUrl.QueueUrl }, cancellationToken);
            }
            catch (Exception)
            {
                // Ignoring any error
            }
        }
        
        async Task DeleteTopicAsync(AmazonSimpleNotificationServiceClient client, string topicName)
        {
            try
            {
                var topic = await client.FindTopicAsync(topicName);
                await client.DeleteTopicAsync(new DeleteTopicRequest
                {
                    TopicArn = topic.TopicArn
                }, cancellationToken);
            }
            catch (Exception)
            {
                // Ignoring any error
            }
        }
    }

    [Fact]
    public async Task When_topic_missing_verify_throws_async()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Publication.MakeChannels = OnMissingChannel.Validate;
        
        
        // act & assert
        try
        {
            Producer = await CreateProducerAsync(Publication);
            Assert.Fail("Expecting an exception");
        }
        catch (Exception e) when(e is not XunitException)
        {
            Assert.IsType<BrokerUnreachableException>(e);
        }
    }
    
    [Fact]
    public async Task When_queues_missing_verify_throws_async()
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
            Producer = await CreateProducerAsync(Publication);
            Channel = await CreateChannelAsync(Subscription);
            Assert.Fail("Expecting an exception");
        }
        catch (Exception e) when(e is not XunitException)
        {
            Assert.IsType<QueueDoesNotExistException>(e);
        }
    }
    
    [Fact]
    public async Task When_queues_missing_assume_throws_async()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!,
            GetOrCreateChannelName(),
            OnMissingChannel.Assume);
        
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);
        
        
        // act & assert
        try
        {
            // Ensure the topic is created
            await Producer.SendAsync(CreateMessage(Publication.Topic!));
            await Channel.ReceiveAsync(ReceiveTimeout);
            Assert.Fail("Expecting an exception");
        }
        catch (Exception e) when(e is not XunitException)
        {
            Assert.IsType<QueueDoesNotExistException>(e);
        }
    }
}
