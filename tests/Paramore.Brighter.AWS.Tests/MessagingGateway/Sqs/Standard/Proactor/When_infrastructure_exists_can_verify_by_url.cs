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

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Proactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class AWSValidateInfrastructureByUrlTests : IAsyncDisposable
{
    private Message _message;
    private IAmAMessageConsumerSync _consumer;
    private SqsMessageProducer _messageProducer;
    private ChannelFactory _channelFactory;
    private MyCommand _myCommand;

    [Before(Test)]
    public async Task Setup()
    {
        _myCommand = new MyCommand { Value = "Test" };
        var replyTo = new RoutingKey("http:\\queueUrl");
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var correlationId = Id.Random();
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);

        SqsSubscription<MyCommand> subscription = new(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(queueName),
            channelType: ChannelType.PointToPoint, routingKey: routingKey, messagePumpType: MessagePumpType.Reactor, makeChannels: OnMissingChannel.Create);

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        //We need to do this manually in a test - will create the channel from subscriber parameters
        //This doesn't look that different from our create tests - this is because we create using the channel factory in
        //our AWS transport, not the consumer (as it's a more likely to use infrastructure declared elsewhere)
        _channelFactory = new ChannelFactory(awsConnection);
        var channel = _channelFactory.CreateSyncChannel(subscription);

        var queueUrl = await FindQueueUrl(awsConnection, routingKey.Value);

        //Now change the subscription to validate, just check what we made
        subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channel.Name,
            routingKey: routingKey,
            findQueueBy: QueueFindBy.Url,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Validate,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }));

        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication
            {
                Topic = routingKey,
                ChannelName= new ChannelName(queueUrl),
                FindQueueBy = QueueFindBy.Url,
                MakeChannels = OnMissingChannel.Validate
            });

        _consumer = new SqsMessageConsumerFactory(awsConnection).Create(subscription);
    }

    [Test]
    public async Task When_infrastructure_exists_can_verify()
    {
        //arrange
        await _messageProducer.SendAsync(_message);

        await Task.Delay(1000);

        var messages = _consumer.Receive(TimeSpan.FromMilliseconds(5000));

        //Assert
        var message = messages.First();
        await Assert.That(message.Id).IsEqualTo(_myCommand.Id);

        //clear the queue
        _consumer.Acknowledge(message);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        //Clean up resources that we have created
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        _consumer.Dispose();
        await _messageProducer.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await ((IAmAMessageConsumerAsync)_consumer).DisposeAsync();
        await _messageProducer.DisposeAsync();
    }

    private static async Task<string> FindQueueUrl(AWSMessagingGatewayConnection connection, string queueName)
    {
        using var snsClient = new AWSClientFactory(connection).CreateSqsClient();
        var topicResponse = await snsClient.GetQueueUrlAsync(queueName);
        return topicResponse.QueueUrl;
    }
}
