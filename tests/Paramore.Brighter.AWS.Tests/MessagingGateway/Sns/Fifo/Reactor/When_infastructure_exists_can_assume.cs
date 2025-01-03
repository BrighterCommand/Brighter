using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Fifo.Reactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class AWSAssumeInfrastructureTests : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly SqsMessageConsumer _consumer;
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public AWSAssumeInfrastructureTests()
    {
        _myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        const string contentType = "text\\plain";
        var correlationId = Guid.NewGuid().ToString();
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var routingKey = new RoutingKey(topicName);

        var subscription = new SqsSubscription<MyCommand>(
            name: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create,
            sqsType: SnsSqsType.Fifo);

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
        subscription = new(
            name: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Assume,
            sqsType: SnsSqsType.Fifo
        );

        _messageProducer = new SnsMessageProducer(awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Assume, SnsAttributes = new SnsAttributes { Type = SnsSqsType.Fifo }
            });

        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(true));
    }

    [Fact]
    public void When_infastructure_exists_can_assume()
    {
        //arrange
        _messageProducer.Send(_message);

        var messages = _consumer.Receive(TimeSpan.FromMilliseconds(5000));

        //Assert
        var message = messages.First();
        message.Id.Should().Be(_myCommand.Id);

        //clear the queue
        _consumer.Acknowledge(message);
    }

    public void Dispose()
    {
        //Clean up resources that we have created
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
