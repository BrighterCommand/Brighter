using System;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Standard.Proactor;

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
        string correlationId = Id.Random();
        var replyTo = new RoutingKey("http:\\queueUrl");
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey($"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45));

        SqsSubscription<MyCommand> subscription = new(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }),
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]));

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
        var awsConnection = GatewayFactory.CreateFactory(credentials, region);

        _channelFactory = new ChannelFactory(awsConnection);
        var channel = await _channelFactory.CreateAsyncChannelAsync(subscription);

        var topicArn = await FindTopicArn(awsConnection, routingKey.Value);
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
                MakeChannels = OnMissingChannel.Validate
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
