using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using System.Collections.Generic;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Fifo.Reactor;

[Category("AWS")]
public class SqsMessageProducerSendAsyncTests : IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;
    private readonly Id _correlationId;
    private readonly RoutingKey _replyTo;
    private readonly ContentType _contentType;
    private readonly string _queueName;
    private readonly string _messageGroupId;
    private readonly string _deduplicationId;

    public SqsMessageProducerSendAsyncTests()
    {
        _myCommand = new MyCommand { Value = "Test" };
        _correlationId = Guid.NewGuid().ToString();
        _replyTo = new RoutingKey("http:\\queueUrl");
        _contentType = new ContentType(MediaTypeNames.Text.Plain){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()};
        _messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        _deduplicationId = $"DeduplicationId{Guid.NewGuid():N}";
        _queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(_queueName);

        var channelName = new ChannelName(_queueName);
        var queueAttributes = new SqsAttributes(type:SqsType.Fifo, rawMessageDelivery: true, tags: new Dictionary<string, string> { { "Environment", "Test" } });

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(_queueName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey, 
            messagePumpType: MessagePumpType.Reactor, 
            queueAttributes: queueAttributes,
            makeChannels: OnMissingChannel.Create
            );

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: _correlationId,
                replyTo: new RoutingKey(_replyTo), contentType: _contentType, partitionKey: _messageGroupId)
            {
                Bag = { [HeaderNames.DeduplicationId] = _deduplicationId }
            },
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        _channel = _channelFactory.CreateSyncChannel(subscription);

        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication(channelName: channelName, makeChannels: OnMissingChannel.Create, queueAttributes: queueAttributes)
        );
    }

    [Test]
    public async Task When_posting_a_message_via_the_producer()
    {
        // arrange
        _message.Header.Subject = "test subject";
        await _messageProducer.SendAsync(_message);

        await Task.Delay(1000);

        var message =  _channel.Receive(TimeSpan.FromMilliseconds(5000));

        // clear the queue
        _channel.Acknowledge(message);

        // should_send_the_message_to_aws_sqs
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);

        await Assert.That(message.Id).IsEqualTo(_myCommand.Id);
        await Assert.That(message.Redelivered).IsFalse();
        await Assert.That(message.Header.MessageId).IsEqualTo(_myCommand.Id);
        await Assert.That(message.Header.Topic.Value).Contains(_queueName);
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

        await Assert.That(message.Header.PartitionKey).IsEqualTo(_messageGroupId);
        await Assert.That(message.Header.Bag).ContainsKey(HeaderNames.DeduplicationId);
        await Assert.That(message.Header.Bag[HeaderNames.DeduplicationId]).IsEqualTo(_deduplicationId);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        //Clean up resources that we have created
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _messageProducer.DisposeAsync();
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

