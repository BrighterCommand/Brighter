using System;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using System.Collections.Generic;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Standard.Proactor;

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
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);

        var channelName = new ChannelName(queueName);
        
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }));

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var channel = await _channelFactory.CreateAsyncChannelAsync(subscription);

        var queueUrl = await FindQueueUrl(awsConnection, routingKey.Value);

        subscription = new(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            findTopicBy: TopicFindBy.Arn,
            makeChannels: OnMissingChannel.Validate
        );

        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication(
                channelName: new ChannelName(queueUrl), 
                findQueueBy: QueueFindBy.Url,
                makeChannels: OnMissingChannel.Validate) 
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
        var response = await snsClient.GetQueueUrlAsync(queueName);
        return response.QueueUrl;
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
