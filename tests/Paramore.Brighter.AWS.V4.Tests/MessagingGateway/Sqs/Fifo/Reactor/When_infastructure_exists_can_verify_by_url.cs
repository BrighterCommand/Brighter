using System;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Fifo.Reactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class AwsValidateInfrastructureByUrlTests : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public AwsValidateInfrastructureByUrlTests ()
    {
        var replyTo = new RoutingKey("http:\\queueUrl");
        var contentType = new ContentType(MediaTypeNames.Text.Plain);

        _myCommand = new MyCommand { Value = "Test" };
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var routingKey = new RoutingKey(queueName);

        var channelName = new ChannelName(queueName);
        var queueAttributes = new SqsAttributes(
            type: SqsType.Fifo
        );
        
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey, 
            messagePumpType: MessagePumpType.Reactor, 
            queueAttributes: queueAttributes, 
            makeChannels: OnMissingChannel.Create);

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType, partitionKey: messageGroupId),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        //We need to do this manually in a test - will create the channel from subscriber parameters
        //This doesn't look that different from our create tests - this is because we create using the channel factory in
        //our AWS transport, not the consumer (as it's a more likely to use infrastructure declared elsewhere)
        _channelFactory = new ChannelFactory(awsConnection);
        var channel = _channelFactory.CreateSyncChannel(subscription);

        var queueUrl = FindQueueUrl(awsConnection, routingKey.ToValidSQSQueueName(true));

        //Now change the subscription to validate, just check what we made
        subscription.MakeChannels = OnMissingChannel.Validate;
        subscription.FindQueueBy = QueueFindBy.Url;
        subscription.ChannelName = new ChannelName(queueUrl);

        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication (
                channelName: new ChannelName(queueUrl), 
                queueAttributes: queueAttributes, 
                findQueueBy: QueueFindBy.Url, 
                makeChannels: OnMissingChannel.Validate)
            );

        _consumer = new SqsMessageConsumerFactory(awsConnection).Create(subscription);
    }

    [Fact]
    public async Task When_infrastructure_exists_can_verify()
    {
        //arrange
        _messageProducer.Send(_message);

        await Task.Delay(1000);

        var messages = _consumer.Receive(TimeSpan.FromMilliseconds(5000));

        //Assert
        var message = messages.First();
        Assert.Equal(_myCommand.Id, message.Id);

        //clear the queue
        _consumer.Acknowledge(message);
    }

    public void Dispose()
    {
        //Clean up resources that we have created
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        _consumer.Dispose();
        _messageProducer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await ((IAmAMessageConsumerAsync)_consumer).DisposeAsync();
        await _messageProducer.DisposeAsync();
    }

    private static string FindQueueUrl(AWSMessagingGatewayConnection connection, string queueName)
    {
         using var snsClient = new AWSClientFactory(connection).CreateSqsClient();
        var topicResponse = snsClient.GetQueueUrlAsync(queueName).GetAwaiter().GetResult();
        return topicResponse.QueueUrl;
    }
}
