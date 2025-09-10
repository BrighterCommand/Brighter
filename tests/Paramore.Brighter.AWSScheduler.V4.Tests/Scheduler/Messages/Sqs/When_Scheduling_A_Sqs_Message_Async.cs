using System.Net.Mime;
using Paramore.Brighter.AWSScheduler.V4.Tests.Helpers;
using Paramore.Brighter.AWSScheduler.V4.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.AWS.V4;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;

namespace Paramore.Brighter.AWSScheduler.V4.Tests.Scheduler.Messages.Sqs;

[Trait("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 per test)
[Collection("Scheduler SQS")]
public class SqsSchedulingAsyncMessageTest : IAsyncDisposable
{
    private readonly ContentType _contentType = new(MediaTypeNames.Text.Plain);
    private const int BufferSize = 3;
    private readonly SqsMessageProducer _messageProducer;
    private readonly SqsMessageConsumer _consumer;
    private readonly string _queueName;
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAMessageSchedulerFactory _factory;

    public SqsSchedulingAsyncMessageTest()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var subscriptionName = $"Buffered-Scheduler-Async-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _queueName = $"Buffered-Scheduler-Async-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var routingKey = new RoutingKey(_queueName);

        var channel = _channelFactory.CreateAsyncChannelAsync(new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(_queueName),
            channelType: ChannelType.PointToPoint, routingKey: routingKey, bufferSize: BufferSize, makeChannels: OnMissingChannel.Create)).GetAwaiter().GetResult();

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);
        _messageProducer = new SqsMessageProducer(awsConnection,
            new SqsPublication { MakeChannels = OnMissingChannel.Create });

        _factory = new AwsSchedulerFactory(awsConnection, "brighter-scheduler")
        {
            UseMessageTopicAsTarget = true
        };
    }

    [Fact]
    public async Task When_Scheduling_A_Sqs_Message_Async()
    {
        var routingKey = new RoutingKey(_queueName);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content one")
        );

        var scheduler = (IAmAMessageSchedulerAsync)_factory.Create(null!)!;
        await scheduler.ScheduleAsync(message, TimeSpan.FromMinutes(1));

        await Task.Delay(TimeSpan.FromMinutes(1));

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = await _consumer.ReceiveAsync(TimeSpan.FromMinutes(1));
            Assert.Single(messages);

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                Assert.Equal((string?)message.Body.Value, (string?)messages[0].Body.Value);
                Assert.Equivalent(message.Header, messages[0].Header);
                await _consumer.AcknowledgeAsync(messages[0]);
                return;
            }


            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail("The message wasn't fired");
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteQueueAsync();
        await _messageProducer.DisposeAsync();
        await _consumer.DisposeAsync();
    }
}
