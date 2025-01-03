using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Reactor;

[Trait("Category", "AWS")]
public class SqsMessageProducerSendTests : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;
    private readonly string _correlationId;
    private readonly string _replyTo;
    private readonly string _contentType;
    private readonly string _topicName;

    public SqsMessageProducerSendTests()
    {
        _myCommand = new MyCommand{Value = "Test"};
        _correlationId = Guid.NewGuid().ToString();
        _replyTo = "http:\\queueUrl";
        _contentType = "text\\plain";
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(_topicName);
            
        SqsSubscription<MyCommand> subscription = new(
            name: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            rawMessageDelivery: false
        );
            
        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: _correlationId,
                replyTo: new RoutingKey(_replyTo), contentType: _contentType),
            new MessageBody(JsonSerializer.Serialize((object) _myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();
            
        _channelFactory = new ChannelFactory(awsConnection);
        _channel = _channelFactory.CreateSyncChannel(subscription);
            
        _messageProducer = new SnsMessageProducer(awsConnection, new SnsPublication{Topic = new RoutingKey(_topicName), MakeChannels = OnMissingChannel.Create});
    }

    [Fact]
    public async Task When_posting_a_message_via_the_producer()
    {
        //arrange
        _message.Header.Subject = "test subject";
        _messageProducer.Send(_message);

        await Task.Delay(1000);
            
        var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));
            
        //clear the queue
        _channel.Acknowledge(message);

        //should_send_the_message_to_aws_sqs
        message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);

        message.Id.Should().Be(_myCommand.Id);
        message.Redelivered.Should().BeFalse();
        message.Header.MessageId.Should().Be(_myCommand.Id);
        message.Header.Topic.Value.Should().Contain(_topicName);
        message.Header.CorrelationId.Should().Be(_correlationId);
        message.Header.ReplyTo.Should().Be(_replyTo);
        message.Header.ContentType.Should().Be(_contentType);
        message.Header.HandledCount.Should().Be(0);
        message.Header.Subject.Should().Be(_message.Header.Subject);
        //allow for clock drift in the following test, more important to have a contemporary timestamp than anything
        message.Header.TimeStamp.Should().BeAfter(RoundToSeconds(DateTime.UtcNow.AddMinutes(-1)));
        message.Header.Delayed.Should().Be(TimeSpan.Zero);
        //{"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
        message.Body.Value.Should().Be(_message.Body.Value);
    }

    public void Dispose()
    {
        //Clean up resources that we have created
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        _messageProducer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _messageProducer.DisposeAsync();
    }
        
    private static DateTime RoundToSeconds(DateTime dateTime)
    {
        return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
    }

}
