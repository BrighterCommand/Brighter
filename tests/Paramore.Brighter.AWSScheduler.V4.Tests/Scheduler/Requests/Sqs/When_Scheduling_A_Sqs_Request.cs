using System.Text.Json;
using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using Paramore.Brighter.AWSScheduler.V4.Tests.Helpers;
using Paramore.Brighter.AWSScheduler.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageScheduler.AWS.V4;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;

namespace Paramore.Brighter.AWSScheduler.V4.Tests.Scheduler.Requests.Sqs;

[Property("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 per test)
public class SqsSchedulingRequestTest
{
    private const int BufferSize = 3;
    private readonly SqsMessageProducer _messageProducer;
    private readonly SqsMessageConsumer _consumer;
    private readonly string _queueName;
    private readonly ChannelFactory _channelFactory;
    private readonly AwsSchedulerFactory _factory;
    private readonly IAmazonScheduler _scheduler;

    public SqsSchedulingRequestTest()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var subscriptionName = $"Buffered-FSR-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _queueName = $"Buffered-FSR-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

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
    public async Task When_Scheduling_A_Sqs_Request_With_Delay_Async(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateAsync(null!);
        var id = await scheduler.ScheduleAsync(command, schedulerType, TimeSpan.FromMinutes(1));
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
                await Assert.That((bool)m.Async).IsTrue();
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
    public async Task When_Scheduling_A_Sqs_Request_With_SpecificDateTimeOffset_Async(
        RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateAsync(null!);
        var id = await scheduler.ScheduleAsync(command, schedulerType,
            DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));

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
                await Assert.That((bool)m.Async).IsTrue();
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
    public async Task When_Rescheduling_A_Sqs_Request_With_Delay_Async(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateAsync(null!);
        var id = await scheduler.ScheduleAsync(command, schedulerType, TimeSpan.FromMinutes(1));
        await Assert.That(Enumerable.Any<char>(id)).IsTrue();

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsScheduler).IsNotNull();

        await Assert.That((bool)(await scheduler.ReSchedulerAsync(id, TimeSpan.FromMinutes(2)))).IsTrue();

        var awsReScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsReScheduler).IsNotNull();
        await Assert.That(awsReScheduler.ScheduleExpression).IsNotEqualTo(awsScheduler.ScheduleExpression);

        await _scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = id });
    }

    [Test]
    [Arguments(RequestSchedulerType.Send)]
    [Arguments(RequestSchedulerType.Post)]
    [Arguments(RequestSchedulerType.Publish)]
    public async Task When_Rescheduling_A_Sqs_Request_With_SpecificDateTimeOffset_Async(
        RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateAsync(null!);
        var id = await scheduler.ScheduleAsync(command, schedulerType,
            DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));
        await Assert.That(Enumerable.Any<char>(id)).IsTrue();

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsScheduler).IsNotNull();

        await Assert.That((bool)(await scheduler.ReSchedulerAsync(id, DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(2))))).IsTrue();

        var awsReScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsReScheduler).IsNotNull();
        await Assert.That(awsReScheduler.ScheduleExpression).IsNotEqualTo(awsScheduler.ScheduleExpression);

        await _scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = id });
    }

    [Test]
    public async Task When_Rescheduling_A_Sqs_Request_That_Not_Exists_Async()
    {
        var scheduler = _factory.CreateAsync(null!);
        await Assert.That((bool)(await scheduler.ReSchedulerAsync(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddHours(1)))).IsFalse();

        await Assert.That((bool)(await scheduler.ReSchedulerAsync(Guid.NewGuid().ToString("N"), TimeSpan.FromMinutes(1)))).IsFalse();
    }

    [Test]
    [Arguments(RequestSchedulerType.Send)]
    [Arguments(RequestSchedulerType.Post)]
    [Arguments(RequestSchedulerType.Publish)]
    public async Task When_Cancel_A_Sqs_Request_Async(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateAsync(null!);
        var id = await scheduler.ScheduleAsync(command, schedulerType,
            DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));
        await Assert.That((bool?)(id)?.Any()).IsTrue();

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        await Assert.That(awsScheduler).IsNotNull();

        await scheduler.CancelAsync(id);

        var ex = await Catch.ExceptionAsync(async () =>
            await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id }));

        await Assert.That(ex).IsNotNull();
        await Assert.That((ex) is ResourceNotFoundException).IsTrue();
    }


    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteQueueAsync();
        await _messageProducer.DisposeAsync();
        await _consumer.DisposeAsync();
        _scheduler.Dispose();
    }
}


