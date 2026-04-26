using System.Net.Mime;
using Paramore.Brighter.AWSScheduler.Tests.Helpers;
using Paramore.Brighter.AWSScheduler.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.AWS;
using Amazon.SimpleNotificationService.Model;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWSScheduler.Tests.Scheduler.Messages.Sns;

[Property("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 minutes per test)
public class SnsSchedulingAsyncMessageTest : IAsyncDisposable
{
    private readonly ContentType _contentType = new(MediaTypeNames.Text.Plain);
    private const int BufferSize = 3;
    private SnsMessageProducer _messageProducer;
    private SqsMessageConsumer _consumer;
    private readonly string _topicName;
    private readonly ChannelFactory _channelFactory;
    private IAmAMessageSchedulerFactory _factory;
    private readonly AWSMessagingGatewayConnection _awsConnection;

    public SnsSchedulingAsyncMessageTest()
    {
        _awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(_awsConnection);
        _topicName = $"Producer-Scheduler-Async-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
    }

    [Before(Test)]
    public async Task Setup()
    {
        //we need the channel to create the queues and notifications
        var channelName = $"Producer-Scheduler-Async-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(_topicName);

        var channel = await _channelFactory.CreateAsyncChannelAsync(new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            bufferSize: BufferSize,
            makeChannels: OnMissingChannel.Create,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } })
        ));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(_awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);
        _messageProducer = new SnsMessageProducer(_awsConnection,
            new SnsPublication { MakeChannels = OnMissingChannel.Create, TopicAttributes = new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]) });

        // Enforce topic to be created
        await _messageProducer.SendAsync(new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content one")
        ));

        await _consumer.PurgeAsync();

        _factory = new AwsSchedulerFactory(_awsConnection, $"brighter-scheduler-{Guid.NewGuid():N}")
        {
            UseMessageTopicAsTarget = true,
            MakeRole = OnMissingRole.Create
        };
    }

    [Test]
    public async Task When_Scheduling_A_Sns_Message_Async()
    {
        var routingKey = new RoutingKey(_topicName);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content one")
        );

        var scheduler = (IAmAMessageSchedulerAsync)_factory.Create(null!);
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
        await _channelFactory.DeleteTopicAsync();
        await _messageProducer.DisposeAsync();
        await _consumer.DisposeAsync();
    }
}


