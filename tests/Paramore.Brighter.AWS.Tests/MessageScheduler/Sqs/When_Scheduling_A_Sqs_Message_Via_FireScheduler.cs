using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Aws;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Scheduler.Events;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessageScheduler.Sqs;

[Collection("Scheduler SQS")]
public class SqsSchedulingMessageViaFireSchedulerTest : IDisposable
{
    private const string ContentType = "text\\plain";
    private const int BufferSize = 3;
    private readonly SqsMessageProducer _messageProducer;
    private readonly SqsMessageConsumer _consumer;
    private readonly string _queueName;
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAMessageSchedulerFactory _factory;

    public SqsSchedulingMessageViaFireSchedulerTest()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var subscriptionName = $"Buffered-Scheduler-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _queueName = $"Buffered-Scheduler-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var routingKey = new RoutingKey(_queueName);

        var channel = _channelFactory.CreateSyncChannel(new SqsSubscription<MyCommand>(
            name: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(_queueName),
            routingKey: routingKey,
            bufferSize: BufferSize,
            makeChannels: OnMissingChannel.Create,
            channelType: ChannelType.PointToPoint
        ));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);
        _messageProducer = new SqsMessageProducer(awsConnection,
            new SqsPublication { MakeChannels = OnMissingChannel.Create });

        _factory = new AwsMessageSchedulerFactory(awsConnection, "brighter-scheduler")
        {
            UseMessageTopicAsTarget = false, 
            MakeRole = OnMissingRole.CreateRole,
            SchedulerTopicOrQueue = routingKey
        };
    }

    [Fact]
    public void When_Scheduling_A_Sqs_Message_Via_FireScheduler()
    {
        var routingKey = new RoutingKey(_queueName);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: ContentType),
            new MessageBody("test content one")
        );

        var scheduler = (IAmAMessageSchedulerSync)_factory.Create(null!)!;
        scheduler.Schedule(message, TimeSpan.FromMinutes(1));

        Task.Delay(TimeSpan.FromMinutes(1)).Wait();

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = _consumer.Receive(TimeSpan.FromMinutes(1));
            messages.Should().ContainSingle();

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                messages[0].Header.MessageType.Should().Be(MessageType.MT_COMMAND);
                messages[0].Body.Value.Should().NotBeNullOrEmpty();
                var m = JsonSerializer.Deserialize<FireSchedulerMessage>(messages[0].Body.Value,
                    JsonSerialisationOptions.Options);
                m.Should().NotBeNull();
                m.Message.Should().BeEquivalentTo(message);
                m.Async.Should().BeFalse();
                _consumer.Acknowledge(messages[0]);
                return;
            }

            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
        }

        Assert.Fail("The message wasn't fired");
    }

    public void Dispose()
    {
        _channelFactory.DeleteQueueAsync().GetAwaiter().GetResult();
        _messageProducer.Dispose();
        _consumer.Dispose();
    }
}
