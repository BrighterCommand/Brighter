using System.Net.Mime;
using System.Text.Json;
using Amazon.Scheduler;
using Paramore.Brighter.AWSScheduler.Tests.Helpers;
using Paramore.Brighter.AWSScheduler.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageScheduler.AWS;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWSScheduler.Tests.Scheduler.Requests.Sqs;

[Property("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 per test)
public class SqsSchedulingRequestAsyncTest : IAsyncDisposable
{
    private readonly ContentType _contentType = new(MediaTypeNames.Text.Plain);
    private const int BufferSize = 3;
    private SqsMessageProducer _messageProducer;
    private SqsMessageConsumer _consumer;
    private readonly string _queueName;
    private readonly ChannelFactory _channelFactory;
    private AwsSchedulerFactory _factory;
    private IAmazonScheduler _scheduler;
    private readonly AWSMessagingGatewayConnection _awsConnection;

    public SqsSchedulingRequestAsyncTest()
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
        var sqsAttributes = new SqsAttributes(
            messageRetentionPeriod: TimeSpan.FromMinutes(1),
            lockTimeout: TimeSpan.FromSeconds(30),
            timeOut: TimeSpan.FromSeconds(30),
            delaySeconds: TimeSpan.Zero,
            tags: new Dictionary<string, string> { { "Environment", "Test" } }
        );

        var channel = await _channelFactory.CreateAsyncChannelAsync(new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(_queueName),
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey, bufferSize: BufferSize, queueAttributes: sqsAttributes, makeChannels: OnMissingChannel.Create));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(_awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);

        //in principle, for point-to-point, we don't need both sides to create the queue;  whoever does not own the API can just validate
        _messageProducer = new SqsMessageProducer(
            _awsConnection,
            new SqsPublication{ QueueAttributes = sqsAttributes,  MakeChannels = OnMissingChannel.Create });

        _scheduler = new AWSClientFactory(_awsConnection).CreateSchedulerClient();
        _factory = new AwsSchedulerFactory(_awsConnection, $"brighter-scheduler-{Guid.NewGuid():N}")
        {
            UseMessageTopicAsTarget = false, MakeRole = OnMissingRole.Create, SchedulerTopicOrQueue = routingKey
        };
    }

    [Test]
    public async Task When_Scheduling_A_Sqs_Message_Via_FireScheduler_Async()
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
                await Assert.That(messages[0].Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
                await Assert.That(Enumerable.Any<char>(messages[0].Body.Value)).IsTrue();
                var m = JsonSerializer.Deserialize<FireAwsScheduler>(messages[0].Body.Value,
                    JsonSerialisationOptions.Options);
                await Assert.That((object?)m).IsNotNull();
                await Assert.That(m.Message).IsEquivalentTo(message);
                await Assert.That((bool)m.Async).IsTrue();
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
