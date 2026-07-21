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

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Fifo.Reactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class AwsAssumeInfrastructureTests : IAsyncDisposable
{
    private readonly Message _message;
    private readonly SqsMessageConsumer _consumer;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public AwsAssumeInfrastructureTests()
    {
        _myCommand = new MyCommand { Value = "Test" };
       var replyTo = new RoutingKey("http:\\queueUrl");
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var correlationId = Id.Random();
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var routingKey = new RoutingKey(queueName);

        var channelName = new ChannelName(queueName);
        var queueAttributes = new SqsAttributes(
            type: SqsType.Fifo,
            tags: new Dictionary<string, string> { { "Environment", "Test" } });
        
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(queueName),
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

        //Now change the subscription to validate, just check what we made
        subscription.MakeChannels = OnMissingChannel.Assume;

        _messageProducer = new SqsMessageProducer(awsConnection,
            new SqsPublication(channelName: channelName, queueAttributes: queueAttributes, makeChannels: OnMissingChannel.Assume));

        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(true));
    }

    [Test]
    public async Task When_infastructure_exists_can_assume()
    {
        //arrange
        await _messageProducer.SendAsync(_message);

        var messages = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        //Assert
        var message = messages.First();
        await Assert.That(message.Id).IsEqualTo(_myCommand.Id);

        //clear the queue
        await _consumer.AcknowledgeAsync(message);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        //Clean up resources that we have created
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
