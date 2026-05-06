using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.AWSScheduler.V4.Tests.Helpers;
using Paramore.Brighter.AWSScheduler.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageScheduler.AWS.V4;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;

namespace Paramore.Brighter.AWSScheduler.V4.Tests.Scheduler.Messages.Sqs;

[Property("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 per test)
public class SqsSchedulingMessageViaFireSchedulerTest
{
    private readonly ContentType _contentType = new (MediaTypeNames.Text.Plain); 
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
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(_queueName),
            channelType: ChannelType.PointToPoint, routingKey: routingKey, bufferSize: BufferSize, makeChannels: OnMissingChannel.Create,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } })));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);
        _messageProducer = new SqsMessageProducer(awsConnection,
            new SqsPublication { MakeChannels = OnMissingChannel.Create, QueueAttributes = new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }) });

        _factory = new AwsSchedulerFactory(awsConnection, $"brighter-scheduler-{Guid.NewGuid():N}")
        {
            UseMessageTopicAsTarget = false, 
            MakeRole = OnMissingRole.Create,
            SchedulerTopicOrQueue = routingKey
        };
    }

    [Test]
    public async Task When_Scheduling_A_Sqs_Message_Via_FireScheduler()
    {
        var routingKey = new RoutingKey(_queueName);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content one")
        );

        var scheduler = (IAmAMessageSchedulerSync)_factory.Create(null!)!;
        scheduler.Schedule(message, TimeSpan.FromMinutes(1));

        Thread.Sleep(TimeSpan.FromMinutes(1));

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
                await Assert.That(m.Message).IsEqualTo(message);
                await Assert.That((bool)m.Async).IsFalse();
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
        await _messageProducer.DisposeAsync();
        await _consumer.DisposeAsync();
    }
}


