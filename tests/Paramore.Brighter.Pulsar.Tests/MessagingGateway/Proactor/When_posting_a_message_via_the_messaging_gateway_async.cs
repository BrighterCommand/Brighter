using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.Pulsar;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Pulsar.Tests.TestDoubles;
using Paramore.Brighter.Pulsar.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.Pulsar.Tests.MessagingGateway.Proactor;

[Trait("Category", "Pulsar")]
public class MessageProducerSendAsyncTests  : IAsyncDisposable 
{
    private readonly Message _message;
    private readonly IAmAChannelAsync _channel;
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly MyCommand _myCommand;
    private readonly Id _correlationId;
    private readonly RoutingKey _replyTo;

    public MessageProducerSendAsyncTests()
    {
        _myCommand = new MyCommand { Value = "Test" };
        _correlationId = Id.Random();
        _replyTo = new RoutingKey("http:\\queueUrl");
        var contentType = new ContentType (MediaTypeNames.Text.Plain);
        var channelName = Guid.NewGuid().ToString();
        var publication = new PulsarPublication{ Topic = Guid.NewGuid().ToString() };

        var mqSubscription = new PulsarSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: publication.Topic!,
            messagePumpType: MessagePumpType.Proactor
        );

        _message = new Message(
            new MessageHeader(_myCommand.Id, publication.Topic!, MessageType.MT_COMMAND, correlationId: _correlationId,
                replyTo: new RoutingKey(_replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var connection = GatewayFactory.CreateConnection();
        var channelFactory = new PulsarChannelFactory(new PulsarMessageConsumerFactory(connection));
        _channel = channelFactory.CreateAsyncChannel(mqSubscription);
        _messageProducer = new PulsarMessageProducer(
            GatewayFactory.CreateProducer(connection, publication),
            publication, TimeProvider.System, InstrumentationOptions.None);
    }

    [Fact]
    public async Task When_posting_a_message_via_the_producer_async()
    {
        // arrange
        await  _channel.PurgeAsync();
        
        _message.Header.Subject = "test subject";
        await _messageProducer.SendAsync(_message);

        await Task.Delay(1000);

        var message = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        // clear the queue
        await _channel.AcknowledgeAsync(message);

        // should_send_the_message_to_aws_sqs
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);

        Assert.Equal(_myCommand.Id, message.Id);
        Assert.False(message.Redelivered);
        Assert.Equal(_myCommand.Id, message.Header.MessageId);
        Assert.Contains(_messageProducer.Publication.Topic!.Value, message.Header.Topic.Value);
        Assert.Equal(_correlationId, message.Header.CorrelationId);
        Assert.Equal(_replyTo, message.Header.ReplyTo);
        Assert.Equal(0, message.Header.HandledCount);
        Assert.Equal(_message.Header.Subject, message.Header.Subject);
        // allow for clock drift in the following test, more important to have a contemporary timestamp than anything
        Assert.True((message.Header.TimeStamp) > (RoundToSeconds(DateTime.UtcNow.AddMinutes(-1))));
        Assert.Equal(TimeSpan.Zero, message.Header.Delayed);
        // {"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
        Assert.Equal(_message.Body.Value, message.Body.Value);
    }
    
    public async ValueTask DisposeAsync()
    {
        await _messageProducer.DisposeAsync();
    }

    private static DateTime RoundToSeconds(DateTime dateTime)
    {
        return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
    }
}
