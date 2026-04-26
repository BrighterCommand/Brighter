using System.Net.Mime;
using Paramore.Brighter.AWSScheduler.V4.Tests.Helpers;
using Paramore.Brighter.AWSScheduler.V4.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.AWS.V4;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;

namespace Paramore.Brighter.AWSScheduler.V4.Tests.Scheduler.Messages.Sqs;

[Property("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 per test)
public class SqsSchedulingAsyncMessageTest : IAsyncDisposable
{
    private readonly ContentType _contentType = new(MediaTypeNames.Text.Plain);
    private const int BufferSize = 3;
    private SqsMessageProducer _messageProducer;
    private SqsMessageConsumer _consumer;
    private readonly string _queueName;
    private readonly ChannelFactory _channelFactory;
    private IAmAMessageSchedulerFactory _factory;
    private readonly AWSMessagingGatewayConnection _awsConnection;

    public SqsSchedulingAsyncMessageTest()
    {
        _awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(_awsConnection);
        _queueName = $"Buffered-Scheduler-Async-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
    }

    [Before(Test)]
    public async Task Setup()
    {
        var subscriptionName = $"Buffered-Scheduler-Async-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var routingKey = new RoutingKey(_queueName);

        var channel = await _channelFactory.CreateAsyncChannelAsync(new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(_queueName),
            channelType: ChannelType.PointToPoint, routingKey: routingKey, bufferSize: BufferSize, makeChannels: OnMissingChannel.Create,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } })));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(_awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);
        _messageProducer = new SqsMessageProducer(_awsConnection,
            new SqsPublication { MakeChannels = OnMissingChannel.Create, QueueAttributes = new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }) });

        _factory = new AwsSchedulerFactory(_awsConnection, $"brighter-scheduler-{Guid.NewGuid():N}")
        {
            UseMessageTopicAsTarget = true
        };
    }

    [Test]
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
            await Assert.That(messages).HasSingleItem();

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                await Assert.That((string?)messages[0].Body.Value).IsEqualTo((string?)message.Body.Value);
                await Assert.That(messages[0].Header).IsEquivalentTo(message.Header);
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
