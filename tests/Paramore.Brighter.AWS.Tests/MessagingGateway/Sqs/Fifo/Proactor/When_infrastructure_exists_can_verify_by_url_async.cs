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

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Fifo.Proactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class AwsValidateInfrastructureByUrlTestsAsync : IAsyncDisposable
{
    private Message _message;
    private IAmAMessageConsumerAsync _consumer;
    private SqsMessageProducer _messageProducer;
    private ChannelFactory _channelFactory;
    private MyCommand _myCommand;

    [Before(Test)]
    public async Task Setup()
    {
        _myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var correlationId = Id.Random();
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);

        var channelName = new ChannelName(queueName);
        var queueAttributes = new SqsAttributes(type: SqsType.Fifo, tags: new Dictionary<string, string> { { "Environment", "Test" } });
        
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint, 
            routingKey: routingKey, 
            messagePumpType: MessagePumpType.Proactor, 
            queueAttributes: queueAttributes, 
            makeChannels: OnMissingChannel.Create);

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType, partitionKey: messageGroupId),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var channel = await _channelFactory.CreateAsyncChannelAsync(subscription);

        var queueUrl = await FindQueueUrl(awsConnection, routingKey.ToValidSQSQueueName(true));

        subscription.MakeChannels = OnMissingChannel.Validate;

        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication(
                channelName: new ChannelName(queueUrl),
                queueAttributes: queueAttributes,
                findQueueBy: QueueFindBy.Url,
                makeChannels: OnMissingChannel.Validate
                )
            );

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

    private static async Task<string> FindQueueUrl(AWSMessagingGatewayConnection connection, string queueName)
    {
        using var snsClient = new AWSClientFactory(connection).CreateSqsClient();
        var topicResponse = await snsClient.GetQueueUrlAsync(queueName);
        return topicResponse.QueueUrl;
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
