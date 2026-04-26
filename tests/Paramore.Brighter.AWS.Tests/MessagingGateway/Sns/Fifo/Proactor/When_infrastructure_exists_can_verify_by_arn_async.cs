using System;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using System.Collections.Generic;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Fifo.Proactor;

[Category("AWS")]
public class AwsValidateInfrastructureByArnTestsAsync : IAsyncDisposable
{
    private Message _message;
    private IAmAMessageConsumerAsync _consumer;
    private SnsMessageProducer _messageProducer;
    private ChannelFactory _channelFactory;
    private MyCommand _myCommand;

    [Before(Test)]
    public async Task Setup()
    {
        _myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var correlationId = Guid.NewGuid().ToString();
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var routingKey = new RoutingKey($"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45));
        var topicAttributes = new SnsAttributes { Type = SqsType.Fifo };

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            queueAttributes: new SqsAttributes(type: SqsType.Fifo, tags: new Dictionary<string, string> { { "Environment", "Test" } }),
            topicAttributes: topicAttributes,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create);

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType, partitionKey: messageGroupId),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var channel = await _channelFactory.CreateAsyncChannelAsync(subscription);

        var topicArn = await FindTopicArn(awsConnection, routingKey.ToValidSNSTopicName(true));
        var routingKeyArn = new RoutingKey(topicArn);

        subscription.MakeChannels = OnMissingChannel.Validate;
        subscription.RoutingKey = routingKeyArn;
        subscription.FindTopicBy = TopicFindBy.Arn;

        _messageProducer = new SnsMessageProducer(
            awsConnection,
            new SnsPublication
            {
                Topic = routingKey,
                TopicArn = topicArn,
                FindTopicBy = TopicFindBy.Arn,
                MakeChannels = OnMissingChannel.Validate,
                TopicAttributes = topicAttributes
            });

        _consumer = new SqsMessageConsumerFactory(awsConnection).CreateAsync(subscription);
    }

    [Test]
    public async Task When_infrastructure_exists_can_verify_async()
    {
        await _messageProducer.SendAsync(_message);

        await Task.Delay(1000);

        var messages = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        var message = messages.First();
        await Assert.That(message.Id).IsEqualTo(_myCommand.Id);

        await _consumer.AcknowledgeAsync(message);
    }

    private static async Task<string> FindTopicArn(AWSMessagingGatewayConnection connection, string topicName)
    {
        using var snsClient = new AWSClientFactory(connection).CreateSnsClient();
        var topicResponse = await snsClient.FindTopicAsync(topicName);
        return topicResponse.TopicArn;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        //Clean up resources that we have created
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        ((IAmAMessageConsumerSync)_consumer).Dispose();
        await _messageProducer.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _consumer.DisposeAsync();
        await _messageProducer.DisposeAsync();
    }
}
