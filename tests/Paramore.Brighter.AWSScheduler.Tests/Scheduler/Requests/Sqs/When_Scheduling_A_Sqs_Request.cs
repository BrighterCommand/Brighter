using System.Text.Json;
using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using Paramore.Brighter.AWSScheduler.Tests.Helpers;
using Paramore.Brighter.AWSScheduler.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageScheduler.AWS;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWSScheduler.Tests.Scheduler.Requests.Sqs;

[Trait("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 per test)
[Collection("Scheduler SQS")]
public class SqsSchedulingRequestTest : IDisposable
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
            channelType: ChannelType.PointToPoint, routingKey: routingKey, bufferSize: BufferSize, makeChannels: OnMissingChannel.Create));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);
        _messageProducer = new SqsMessageProducer(awsConnection,
            new SqsPublication { MakeChannels = OnMissingChannel.Create });

        _scheduler = new AWSClientFactory(awsConnection).CreateSchedulerClient();

        _factory = new AwsSchedulerFactory(awsConnection, "brighter-scheduler")
        {
            UseMessageTopicAsTarget = false, MakeRole = OnMissingRole.Create, SchedulerTopicOrQueue = routingKey
        };
    }

    [Theory]
    [InlineData(RequestSchedulerType.Send)]
    [InlineData(RequestSchedulerType.Post)]
    [InlineData(RequestSchedulerType.Publish)]
    public async Task When_Scheduling_A_Sqs_Request_With_Delay_Async(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateAsync(null!);
        var id = await scheduler.ScheduleAsync(command, schedulerType, TimeSpan.FromMinutes(1));
        Assert.True(Enumerable.Any<char>(id));

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsScheduler);

        await Task.Delay(TimeSpan.FromMinutes(1));

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = await _consumer.ReceiveAsync(TimeSpan.FromMinutes(1));
            Assert.Single(messages);

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                Assert.Equal(MessageType.MT_COMMAND, messages[0].Header.MessageType);
                Assert.True((bool?)(messages[0].Body.Value)?.Any());
                var m = JsonSerializer.Deserialize<FireAwsScheduler>(messages[0].Body.Value,
                    JsonSerialisationOptions.Options);
                Assert.NotNull((object?)m);
                Assert.Equal(schedulerType, m.SchedulerType);
                Assert.Equal(typeof(MyCommand).FullName, (string?)m.RequestType);
                Assert.True((bool)m.Async);
                await _consumer.AcknowledgeAsync(messages[0]);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail("The message wasn't fired");
    }

    [Theory]
    [InlineData(RequestSchedulerType.Send)]
    [InlineData(RequestSchedulerType.Post)]
    [InlineData(RequestSchedulerType.Publish)]
    public async Task When_Scheduling_A_Sqs_Request_With_SpecificDateTimeOffset_Async(
        RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateAsync(null!);
        var id = await scheduler.ScheduleAsync(command, schedulerType,
            DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsScheduler);

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = await _consumer.ReceiveAsync(TimeSpan.FromMinutes(1));
            Assert.Single(messages);

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                Assert.Equal(MessageType.MT_COMMAND, messages[0].Header.MessageType);
                Assert.True((bool?)(messages[0].Body.Value)?.Any());
                var m = JsonSerializer.Deserialize<FireAwsScheduler>(messages[0].Body.Value,
                    JsonSerialisationOptions.Options);
                Assert.NotNull((object?)m);
                Assert.Equal(schedulerType, m.SchedulerType);
                Assert.Equal(typeof(MyCommand).FullName, (string?)m.RequestType);
                Assert.True((bool)m.Async);
                await _consumer.AcknowledgeAsync(messages[0]);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail("The message wasn't fired");
    }

    [Theory]
    [InlineData(RequestSchedulerType.Send)]
    [InlineData(RequestSchedulerType.Post)]
    [InlineData(RequestSchedulerType.Publish)]
    public async Task When_Rescheduling_A_Sqs_Request_With_Delay_Async(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateAsync(null!);
        var id = await scheduler.ScheduleAsync(command, schedulerType, TimeSpan.FromMinutes(1));
        Assert.True(Enumerable.Any<char>(id));

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsScheduler);

        Assert.True((bool)(await scheduler.ReSchedulerAsync(id, TimeSpan.FromMinutes(2))));

        var awsReScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsReScheduler);
        Assert.NotEqual(awsScheduler.ScheduleExpression, awsReScheduler.ScheduleExpression);

        await _scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = id });
    }

    [Theory]
    [InlineData(RequestSchedulerType.Send)]
    [InlineData(RequestSchedulerType.Post)]
    [InlineData(RequestSchedulerType.Publish)]
    public async Task When_Rescheduling_A_Sqs_Request_With_SpecificDateTimeOffset_Async(
        RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateAsync(null!);
        var id = await scheduler.ScheduleAsync(command, schedulerType,
            DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));
        Assert.True(Enumerable.Any<char>(id));

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsScheduler);

        Assert.True((bool)(await scheduler.ReSchedulerAsync(id, DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(2)))));

        var awsReScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsReScheduler);
        Assert.NotEqual(awsScheduler.ScheduleExpression, awsReScheduler.ScheduleExpression);

        await _scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = id });
    }

    [Fact]
    public async Task When_Rescheduling_A_Sqs_Request_That_Not_Exists_Async()
    {
        var scheduler = _factory.CreateAsync(null!);
        Assert.False((bool)(await scheduler.ReSchedulerAsync(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddHours(1))));

        Assert.False((bool)(await scheduler.ReSchedulerAsync(Guid.NewGuid().ToString("N"), TimeSpan.FromMinutes(1))));
    }

    [Theory]
    [InlineData(RequestSchedulerType.Send)]
    [InlineData(RequestSchedulerType.Post)]
    [InlineData(RequestSchedulerType.Publish)]
    public async Task When_Cancel_A_Sqs_Request_Async(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateAsync(null!);
        var id = await scheduler.ScheduleAsync(command, schedulerType,
            DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));
        Assert.True((bool?)(id)?.Any());

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsScheduler);

        await scheduler.CancelAsync(id);

        var ex = await Catch.ExceptionAsync(async () =>
            await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id }));

        Assert.NotNull(ex);
        Assert.True((ex) is ResourceNotFoundException);
    }


    public void Dispose()
    {
        _channelFactory.DeleteQueueAsync().GetAwaiter().GetResult();
        _messageProducer.Dispose();
        _consumer.Dispose();
        _scheduler.Dispose();
    }
}
