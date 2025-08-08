﻿using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.Pulsar;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Pulsar.Tests.TestDoubles;
using Paramore.Brighter.Pulsar.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.Pulsar.Tests.MessagingGateway.Reactor;

[Trait("Category", "Pulsar")]
public class MessageProducerSendTests  : IDisposable 
{
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly IAmAMessageProducerSync _messageProducer;
    private readonly MyCommand _myCommand;
    private readonly Id _correlationId;
    private readonly RoutingKey _replyTo;

    public MessageProducerSendTests()
    {
        _myCommand = new MyCommand { Value = "Test" };
        _correlationId = Id.Random();
        _replyTo = new RoutingKey("http:\\queueUrl");
        ContentType contentType = new(MediaTypeNames.Text.Plain);
        var channelName = Guid.NewGuid().ToString();
        var publication = new PulsarPublication { Topic = Guid.NewGuid().ToString() };

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
        _channel = channelFactory.CreateSyncChannel(mqSubscription);
        _messageProducer = new PulsarMessageProducer(
            GatewayFactory.CreateProducer(connection, publication), publication,
            TimeProvider.System, InstrumentationOptions.None);
    }

    [Fact]
    public void When_posting_a_message_via_the_producer_async()
    {
        // arrange
        _message.Header.Subject = "test subject";
        _messageProducer.Send(_message);
        
        Thread.Sleep(1000);

        var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

        // clear the queue
        _channel.Acknowledge(message);

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
    
    public void Dispose()
    {
        _channel.Dispose();
        _messageProducer.Dispose();
    }

    private static DateTime RoundToSeconds(DateTime dateTime)
    {
        return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
    }
}
