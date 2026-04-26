using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Namotion.Reflection;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using System.Collections.Generic;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Standard.Proactor;

[Category("AWS")]
public class SqsMessageProducerSendAsyncTests : IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelAsync _channel;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;
    private readonly Id _correlationId;
    private readonly RoutingKey _replyTo;
    private readonly ContentType _contentType;
    private readonly string _queueName;

    public SqsMessageProducerSendAsyncTests()
    {
        _myCommand = new MyCommand { Value = "Test" };
        _correlationId = Guid.NewGuid().ToString();
        _replyTo = new RoutingKey( "http:\\queueUrl");
        _contentType = new ContentType(MediaTypeNames.Text.Plain){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()};
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(_queueName);
        var channelName = new ChannelName(_queueName);

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint, 
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }));

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: _correlationId,
                replyTo: new RoutingKey(_replyTo), contentType: _contentType),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        _channel = _channelFactory.CreateAsyncChannel(subscription);

        _messageProducer = new SqsMessageProducer(awsConnection, new SqsPublication
        {
            ChannelName = channelName,
            MakeChannels = OnMissingChannel.Create
        });
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task When_posting_a_message_via_the_producer_async(bool fairQueue)
    {
        // TODO: remove once Moto pin in #4096 is bumped to 5.1.23+
        Skip.If(fairQueue && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_SERVICE_URL")),
            "SQS fair queues require Moto >= 5.1.23; pinned image is 5.1.22. Runs against real AWS.");

        // arrange
        _message.Header.Subject = "test subject";
        _message.Header.PartitionKey = fairQueue ? new PartitionKey(Uuid.NewAsString()) : PartitionKey.Empty;
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
