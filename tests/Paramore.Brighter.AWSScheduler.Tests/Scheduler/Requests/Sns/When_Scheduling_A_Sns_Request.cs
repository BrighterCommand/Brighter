using System.Net.Mime;
using System.Text.Json;
using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using Paramore.Brighter.AWSScheduler.Tests.Helpers;
using Paramore.Brighter.AWSScheduler.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageScheduler.AWS;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using SnsTag = Amazon.SimpleNotificationService.Model.Tag;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.AWSScheduler.Tests.Scheduler.Requests.Sns;

[Property("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 per test)
public class SnsSchedulingMessageViaFireSchedulerRequestTest
{
    private readonly ContentType _contentType = new(MediaTypeNames.Text.Plain);
    private const int BufferSize = 3;
    private readonly SnsMessageProducer _messageProducer;
    private readonly SqsMessageConsumer _consumer;
    private readonly ChannelFactory _channelFactory;
    private readonly AwsSchedulerFactory _factory;
    private readonly IAmazonScheduler _scheduler;

    public SnsSchedulingMessageViaFireSchedulerRequestTest()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        //we need the channel to create the queues and notifications
        string topicName = $"Producer-FSR-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var channelName = $"Producer-FSR-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        var channel = _channelFactory.CreateSyncChannel(new SqsSubscription<FireSchedulerMessage>(
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
        _messageProducer =
            new SnsMessageProducer(awsConnection, new SnsPublication { MakeChannels = OnMissingChannel.Create, TopicAttributes = new SnsAttributes(tags: [new SnsTag { Key = "Environment", Value = "Test" }]) });

        // Enforce topic to be created
        _messageProducer.Send(new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content one")
        ));
        _consumer.Purge();

        _scheduler = new AWSClientFactory(awsConnection).CreateSchedulerClient();

        _factory = new AwsSchedulerFactory(awsConnection, $"brighter-scheduler-{Guid.NewGuid():N}")
        {
            UseMessageTopicAsTarget = false, MakeRole = OnMissingRole.Create, SchedulerTopicOrQueue = routingKey
        };
    }

    [Test]
    [Arguments(RequestSchedulerType.Send)]
    [Arguments(RequestSchedulerType.Post)]
    [Arguments(RequestSchedulerType.Publish)]
    public async Task When_Scheduling_A_Sns_Request_With_Delay(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateSync(null!);
        var id = scheduler.Schedule(command, schedulerType, TimeSpan.FromMinutes(1));
        await Assert.That(Enumerable.Any<char>(id)).IsTrue();

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsScheduler).IsNotNull();

        await Task.Delay(TimeSpan.FromMinutes(1));

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = await _consumer.ReceiveAsync(TimeSpan.FromMinutes(1));
            await Assert.That(messages).HasSingleItem();

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                await Assert.That(messages[0].Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
                await Assert.That((bool?)(messages[0].Body.Value)?.Any()).IsTrue();
                var m = JsonSerializer.Deserialize<FireAwsScheduler>(messages[0].Body.Value,
                    JsonSerialisationOptions.Options);
                await Assert.That((object?)m).IsNotNull();
                await Assert.That(m.SchedulerType).IsEqualTo(schedulerType);
                await Assert.That((string?)m.RequestType).IsEqualTo(typeof(MyCommand).FullName);
                await Assert.That((bool)m.Async).IsFalse();
                await _consumer.AcknowledgeAsync(messages[0]);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail("The message wasn't fired");
    }

    [Test]
    [Arguments(RequestSchedulerType.Send)]
    [Arguments(RequestSchedulerType.Post)]
    [Arguments(RequestSchedulerType.Publish)]
    public async Task When_Scheduling_A_Sns_Request_With_SpecificDateTime(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateSync(null!);
        var id = scheduler.Schedule(command, schedulerType, DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsScheduler).IsNotNull();

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = await _consumer.ReceiveAsync(TimeSpan.FromMinutes(1));
            
            await Assert.That(messages).HasSingleItem();

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                await Assert.That(messages[0].Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
                await Assert.That((bool?)(messages[0].Body.Value)?.Any()).IsTrue();
                var m = JsonSerializer.Deserialize<FireAwsScheduler>(messages[0].Body.Value,
                    JsonSerialisationOptions.Options);
                await Assert.That((object?)m).IsNotNull();
                await Assert.That(m.SchedulerType).IsEqualTo(schedulerType);
                await Assert.That((string?)m.RequestType).IsEqualTo(typeof(MyCommand).FullName);
                await Assert.That((bool)m.Async).IsFalse();
                await _consumer.AcknowledgeAsync(messages[0]);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail("The message wasn't fired");
    }

    [Test]
    [Arguments(RequestSchedulerType.Send)]
    [Arguments(RequestSchedulerType.Post)]
    [Arguments(RequestSchedulerType.Publish)]
    public async Task When_Rescheduling_A_Sns_Request_With_Delay(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateSync(null!);
        var id = scheduler.Schedule(command, schedulerType, TimeSpan.FromMinutes(1));
        await Assert.That((bool?)(id)?.Any()).IsTrue();

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsScheduler).IsNotNull();

        await Assert.That((bool)scheduler.ReScheduler(id, TimeSpan.FromMinutes(2))).IsTrue();

        var awsReScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsReScheduler).IsNotNull();
        await Assert.That(awsReScheduler.ScheduleExpression).IsNotEqualTo(awsScheduler.ScheduleExpression);

        await _scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = id });
    }

    [Test]
    [Arguments(RequestSchedulerType.Send)]
    [Arguments(RequestSchedulerType.Post)]
    [Arguments(RequestSchedulerType.Publish)]
    public async Task When_Rescheduling_A_Sns_Request_With_SpecificDateTimeOffset(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateSync(null!);
        var id = scheduler.Schedule(command, schedulerType, DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));
        await Assert.That((bool?)(id)?.Any()).IsTrue();

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsScheduler).IsNotNull();

        await Assert.That((bool)scheduler.ReScheduler(id, DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(2)))).IsTrue();

        var awsReScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsReScheduler).IsNotNull();
        await Assert.That(awsReScheduler.ScheduleExpression).IsNotEqualTo(awsScheduler.ScheduleExpression);

        await _scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = id });
    }

    [Test]
    public async Task When_Rescheduling_A_Sns_Request_That_Not_Exists()
    {
        var scheduler = _factory.CreateSync(null!);
        await Assert.That((bool)scheduler.ReScheduler(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddHours(1))).IsFalse();

        await Assert.That((bool)scheduler.ReScheduler(Guid.NewGuid().ToString("N"), TimeSpan.FromMinutes(1))).IsFalse();
    }

    [Test]
    [Arguments(RequestSchedulerType.Send)]
    [Arguments(RequestSchedulerType.Post)]
    [Arguments(RequestSchedulerType.Publish)]
    public async Task When_Cancel_A_Sns_Request(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateSync(null!);
        var id = scheduler.Schedule(command, schedulerType, DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));
        await Assert.That((bool?)(id)?.Any()).IsTrue();

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsScheduler).IsNotNull();

        scheduler.Cancel(id);

        var ex = await Catch.ExceptionAsync(async () =>
            await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id }));

        await Assert.That(ex).IsNotNull();
        await Assert.That((ex) is ResourceNotFoundException).IsTrue();
    }
    
    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteQueueAsync();
        await _channelFactory.DeleteTopicAsync();
        await _messageProducer.DisposeAsync();
        await _consumer.DisposeAsync();
        _scheduler.Dispose();
    }
}


