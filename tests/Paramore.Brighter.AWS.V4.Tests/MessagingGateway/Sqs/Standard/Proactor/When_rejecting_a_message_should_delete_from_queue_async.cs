using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Standard.Proactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class SqsMessageConsumerRejectTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelAsync _channel;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public SqsMessageConsumerRejectTestsAsync()
    {
        _myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);
        var channelName = new ChannelName(queueName);
        
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint, 
            findQueueBy: QueueFindBy.Name,
            routingKey: routingKey, 
            messagePumpType: MessagePumpType.Proactor, 
            makeChannels: OnMissingChannel.Create
            );

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        _channel = _channelFactory.CreateAsyncChannel(subscription);

        _messageProducer =
            new SqsMessageProducer(
                awsConnection, 
                new SqsPublication(channelName,  makeChannels: OnMissingChannel.Create)
                );
    }

    [Fact]
    public async Task When_rejecting_a_message_should_delete_from_queue_async()
    {
        //Arrange
        await _messageProducer.SendAsync(_message);
        var message = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        //Act
        await _channel.RejectAsync(message);

        //Assert - message should be deleted, not requeued
        message = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        Assert.Equal(MessageType.MT_NONE, message.Header.MessageType);
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
