using System.Net.Mime;
using System.Text.Json;
using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using Paramore.Brighter.AWSScheduler.Tests.Helpers;
using Paramore.Brighter.AWSScheduler.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageScheduler.AWS;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.AWSScheduler.Tests.Scheduler.Requests.Sns;

[Trait("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 per test)
[Collection("Scheduler SNS")]
public class SnsSchedulingMessageViaFireSchedulerRequestTest : IDisposable
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
            makeChannels: OnMissingChannel.Create
        ));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);
        _messageProducer =
            new SnsMessageProducer(awsConnection, new SnsPublication { MakeChannels = OnMissingChannel.Create });

        // Enforce topic to be created
        _messageProducer.Send(new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
            new MessageBody("test content one")
        ));
        _consumer.Purge();

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
    public async Task When_Scheduling_A_Sns_Request_With_Delay(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateSync(null!);
        var id = scheduler.Schedule(command, schedulerType, TimeSpan.FromMinutes(1));
        Assert.True(Enumerable.Any<char>(id));

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsScheduler);

        await Task.Delay(TimeSpan.FromMinutes(1));

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = _consumer.Receive(TimeSpan.FromMinutes(1));
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
                Assert.False((bool)m.Async);
                _consumer.Acknowledge(messages[0]);
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
    public async Task When_Scheduling_A_Sns_Request_With_SpecificDateTime(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateSync(null!);
        var id = scheduler.Schedule(command, schedulerType, DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsScheduler);

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = _consumer.Receive(TimeSpan.FromMinutes(1));
            
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
                Assert.False((bool)m.Async);
                _consumer.Acknowledge(messages[0]);
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
    public async Task When_Rescheduling_A_Sns_Request_With_Delay(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateSync(null!);
        var id = scheduler.Schedule(command, schedulerType, TimeSpan.FromMinutes(1));
        Assert.True((bool?)(id)?.Any());

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsScheduler);

        Assert.True((bool)scheduler.ReScheduler(id, TimeSpan.FromMinutes(2)));

        var awsReScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsReScheduler);
        Assert.NotEqual(awsScheduler.ScheduleExpression, awsReScheduler.ScheduleExpression);

        await _scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = id });
    }

    [Theory]
    [InlineData(RequestSchedulerType.Send)]
    [InlineData(RequestSchedulerType.Post)]
    [InlineData(RequestSchedulerType.Publish)]
    public async Task When_Rescheduling_A_Sns_Request_With_SpecificDateTimeOffset(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateSync(null!);
        var id = scheduler.Schedule(command, schedulerType, DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));
        Assert.True((bool?)(id)?.Any());

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsScheduler);

        Assert.True((bool)scheduler.ReScheduler(id, DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(2))));

        var awsReScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsReScheduler);
        Assert.NotEqual(awsScheduler.ScheduleExpression, awsReScheduler.ScheduleExpression);

        await _scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = id });
    }

    [Fact]
    public void When_Rescheduling_A_Sns_Request_That_Not_Exists()
    {
        var scheduler = _factory.CreateSync(null!);
        Assert.False((bool)scheduler.ReScheduler(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddHours(1)));

        Assert.False((bool)scheduler.ReScheduler(Guid.NewGuid().ToString("N"), TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData(RequestSchedulerType.Send)]
    [InlineData(RequestSchedulerType.Post)]
    [InlineData(RequestSchedulerType.Publish)]
    public async Task When_Cancel_A_Sns_Request(RequestSchedulerType schedulerType)
    {
        var command = new MyCommand();

        var scheduler = _factory.CreateSync(null!);
        var id = scheduler.Schedule(command, schedulerType, DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(1)));
        Assert.True((bool?)(id)?.Any());

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        Assert.NotNull(awsScheduler);

        scheduler.Cancel(id);

        var ex = await Catch.ExceptionAsync(async () =>
            await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id }));

        Assert.NotNull(ex);
        Assert.True((ex) is ResourceNotFoundException);
    }
    
    public void Dispose()
    {
        _channelFactory.DeleteQueueAsync().GetAwaiter().GetResult();
        _channelFactory.DeleteTopicAsync().GetAwaiter().GetResult();
        _messageProducer.Dispose();
        _consumer.Dispose();
        _scheduler.Dispose();
    }
}
