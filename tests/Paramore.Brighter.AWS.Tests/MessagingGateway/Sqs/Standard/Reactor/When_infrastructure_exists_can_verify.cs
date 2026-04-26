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

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Reactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class AWSValidateInfrastructureTests : IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public AWSValidateInfrastructureTests()
    {
        _myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(queueName),
            channelType: ChannelType.PointToPoint, routingKey: routingKey, messagePumpType: MessagePumpType.Reactor, makeChannels: OnMissingChannel.Create,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }));

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

        //Now change the subscription to validate, just check what we made
        subscription = new(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channel.Name,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            findTopicBy: TopicFindBy.Name, makeChannels: OnMissingChannel.Validate);

        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication(
                channelName:channel.Name,
                makeChannels: OnMissingChannel.Validate
                )  
        );

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
}
