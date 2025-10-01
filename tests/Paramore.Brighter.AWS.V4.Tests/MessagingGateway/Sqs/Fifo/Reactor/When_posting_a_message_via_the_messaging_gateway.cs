using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Fifo.Reactor;

[Trait("Category", "AWS")]
public class SqsMessageProducerSendAsyncTests : IAsyncDisposable, IDisposable
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
        var queueAttributes = new SqsAttributes(type:SqsType.Fifo, rawMessageDelivery: true);

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

    [Fact]
    public void When_posting_a_message_via_the_producer()
    {
        // arrange
        _message.Header.Subject = "test subject";
        _messageProducer.Send(_message);

        Task.Delay(1000).GetAwaiter().GetResult();

        var message =  _channel.Receive(TimeSpan.FromMilliseconds(5000));

        // clear the queue
        _channel.Acknowledge(message);

        // should_send_the_message_to_aws_sqs
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);

        Assert.Equal(_myCommand.Id, message.Id);
        Assert.False(message.Redelivered);
        Assert.Equal(_myCommand.Id, message.Header.MessageId);
        Assert.Contains(_queueName, message.Header.Topic.Value);
        Assert.Equal(_correlationId, message.Header.CorrelationId);
        Assert.Equal(_replyTo, message.Header.ReplyTo);
        Assert.Equal(_contentType, message.Header.ContentType);
        Assert.Equal(0, message.Header.HandledCount);
        Assert.Equal(_message.Header.Subject, message.Header.Subject);
        // allow for clock drift in the following test, more important to have a contemporary timestamp than anything
        Assert.True((message.Header.TimeStamp) > (RoundToSeconds(DateTime.UtcNow.AddMinutes(-1))));
        Assert.Equal(TimeSpan.Zero, message.Header.Delayed);
        // {"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
        Assert.Equal(_message.Body.Value, message.Body.Value);

        Assert.Equal(_messageGroupId, message.Header.PartitionKey);
        Assert.Contains(HeaderNames.DeduplicationId, message.Header.Bag);
        Assert.Equal(_deduplicationId, message.Header.Bag[HeaderNames.DeduplicationId]);
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

