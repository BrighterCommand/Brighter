using System.Net.Mime;
using Paramore.Brighter.AWSScheduler.Tests.Helpers;
using Paramore.Brighter.AWSScheduler.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.AWS;
using Amazon.SimpleNotificationService.Model;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWSScheduler.Tests.Scheduler.Messages.Sns;

[Property("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 per test)
public class SnsSchedulingMessageTest
{
    private readonly ContentType _contentType = new ContentType(MediaTypeNames.Text.Plain);
    private const int BufferSize = 3;
    private readonly SnsMessageProducer _messageProducer;
    private readonly SqsMessageConsumer _consumer;
    private readonly string _topicName;
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAMessageSchedulerFactory _factory;

    public SnsSchedulingMessageTest()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        //we need the channel to create the queues and notifications
        _topicName = $"Producer-Scheduler-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var channelName = $"Producer-Scheduler-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(_topicName);

        var channel = _channelFactory.CreateSyncChannel(new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            bufferSize: BufferSize,
            makeChannels: OnMissingChannel.Create,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } })
        ));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);
        _messageProducer = new SnsMessageProducer(awsConnection,
            new SnsPublication { MakeChannels = OnMissingChannel.Create, TopicAttributes = new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]) });

        // Enforce topic to be created
        _messageProducer.Send(new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content one")
        ));

        _consumer.Purge();

        _factory = new AwsSchedulerFactory(awsConnection, $"brighter-scheduler-{Guid.NewGuid():N}")
        {
            UseMessageTopicAsTarget = true, MakeRole = OnMissingRole.Create
        };
    }

    [Test]
    public async Task When_Scheduling_A_Sns_Message()
    {
        var routingKey = new RoutingKey(_topicName);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content one")
        );

        var scheduler = (IAmAMessageSchedulerSync)_factory.Create(null!);
        scheduler.Schedule(message, TimeSpan.FromMinutes(1));

        Thread.Sleep(TimeSpan.FromMinutes(1));

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = await _consumer.ReceiveAsync();
            await Assert.That(messages).HasSingleItem();

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                await Assert.That(messages[0].Header.MessageType).IsEqualTo(message.Header.MessageType);
                await Assert.That((string?)messages[0].Body.Value).IsEqualTo((string?)message.Body.Value);
                await Assert.That(messages[0].Header).IsEquivalentTo(message.Header);
                await _consumer.AcknowledgeAsync(messages[0]);
                return;
            }

            Thread.Sleep(TimeSpan.FromSeconds(1));
        }

        Assert.Fail("The message wasn't fired");
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteQueueAsync();
        await _channelFactory.DeleteTopicAsync();
        await _messageProducer.DisposeAsync();
        await _consumer.DisposeAsync();
    }
}


