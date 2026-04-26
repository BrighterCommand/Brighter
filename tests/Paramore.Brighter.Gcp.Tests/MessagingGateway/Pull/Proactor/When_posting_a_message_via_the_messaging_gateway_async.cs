using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull.Proactor;

[Category("GCP")]
public class MessageProducerSendAsyncTests : IDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelAsync _channel;
    private readonly GcpMessageProducer _messageProducer;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;
    private readonly Id _correlationId;
    private readonly RoutingKey _replyTo;
    private readonly ContentType _contentType;
    private readonly string _topicName;
    private readonly GcpPubSubSubscription<MyCommand> _pubSubSubscription;

    public MessageProducerSendAsyncTests()
    {
        _myCommand = new MyCommand { Value = "Test" };
        _correlationId = Id.Random();
        _replyTo = new RoutingKey("http:\\queueUrl");
        _contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(_topicName);

         _pubSubSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            subscriptionMode: SubscriptionMode.Pull
        );

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: _correlationId,
                replyTo: new RoutingKey(_replyTo), contentType: _contentType),
            new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
        );

        _channelFactory = GatewayFactory.CreateChannelFactory();
        _channel = _channelFactory.CreateAsyncChannel(_pubSubSubscription);
        _messageProducer = GatewayFactory.CreateProducer(new GcpPublication<MyCommand>
            {
                Topic = new RoutingKey(_topicName), 
                MakeChannels = OnMissingChannel.Create
            });
    }

    [Test]
    public async Task When_posting_a_message_via_the_producer_async()
    {
        // arrange
        _message.Header.Subject = "test subject";
        await _messageProducer.SendAsync(_message);

        await Task.Delay(1000);

        var message = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        // clear the queue
        await _channel.AcknowledgeAsync(message);

        // should_send_the_message_to_aws_sqs
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);

        await Assert.That(message.Id).IsEqualTo(_myCommand.Id);
        await Assert.That(message.Redelivered).IsFalse();
        await Assert.That(message.Header.MessageId).IsEqualTo(_myCommand.Id);
        await Assert.That(message.Header.Topic.Value).Contains(_topicName);
        await Assert.That(message.Header.CorrelationId).IsEqualTo(_correlationId);
        await Assert.That(message.Header.ReplyTo).IsEqualTo(_replyTo);
        await Assert.That(message.Header.ContentType).IsEqualTo(_contentType);
        await Assert.That(message.Header.HandledCount).IsEqualTo(0);
        await Assert.That(message.Header.Subject).IsEqualTo(_message.Header.Subject);
        // allow for clock drift in the following test, more important to have a contemporary timestamp than anything
        await Assert.That((message.Header.TimeStamp) > (RoundToSeconds(DateTime.UtcNow.AddMinutes(-1)))).IsTrue();
        await Assert.That(message.Header.Delayed).IsEqualTo(TimeSpan.Zero);
        // {"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
        await Assert.That(message.Body.Value).IsEqualTo(_message.Body.Value);
    }
        
    public void Dispose()
    {
        _channelFactory.DeleteTopic(_pubSubSubscription);
        _channelFactory.DeleteSubscription(_pubSubSubscription);
        _messageProducer.Dispose();
    }

    private static DateTime RoundToSeconds(DateTime dateTime)
    {
        return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
    }
}
