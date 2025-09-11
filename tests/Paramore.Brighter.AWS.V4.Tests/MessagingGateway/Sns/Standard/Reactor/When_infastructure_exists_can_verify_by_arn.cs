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
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Standard.Reactor;

[Trait("Category", "AWS")]
public class AwsValidateInfrastructureByArnTests : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAMessageConsumerAsync _consumer;
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public AwsValidateInfrastructureByArnTests()
    {
        _myCommand = new MyCommand { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey($"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45));

        SqsSubscription<MyCommand> subscription = new(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create
        );

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
        var awsConnection = GatewayFactory.CreateFactory(credentials, region);

        //We need to do this manually in a test - will create the channel from subscriber parameters
        //This doesn't look that different from our create tests - this is because we create using the channel factory in
        //our AWS transport, not the consumer (as it's a more likely to use infrastructure declared elsewhere)
        _channelFactory = new ChannelFactory(awsConnection);
        var channel = _channelFactory.CreateAsyncChannel(subscription);

        var topicArn = FindTopicArn(awsConnection, routingKey.Value);
        var routingKeyArn = new RoutingKey(topicArn);

        //Now change the subscription to validate, just check what we made
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

    [Fact]
    public async Task When_infrastructure_exists_can_verify()
    {
        //arrange
        await _messageProducer.SendAsync(_message);

        await Task.Delay(1000);

        var messages = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        //Assert
        var message = messages.First();
        Assert.NotEqual(MessageType.MT_NONE, message.Header.MessageType);
        Assert.Equal(_myCommand.Id, message.Id);

        //clear the queue
        await _consumer.AcknowledgeAsync(message);
    }

    public void Dispose()
    {
        //Clean up resources that we have created
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        ((IAmAMessageConsumerSync)_consumer).Dispose();
        _messageProducer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _consumer.DisposeAsync();
        await _messageProducer.DisposeAsync();
    }

    private static string FindTopicArn(AWSMessagingGatewayConnection connection, string topicName)
    {
        using var snsClient = new AWSClientFactory(connection).CreateSnsClient();
        var topicResponse = snsClient.FindTopicAsync(topicName).GetAwaiter().GetResult();
        return topicResponse.TopicArn;
    }
}
