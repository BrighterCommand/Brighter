using System;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Aws;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Scheduler.Events;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.Scheduler.Requests.Sns;

[Trait("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 per test)
[Collection("Scheduler SNS")]
public class SnsSchedulingMessageViaFireSchedulerRequestTest : IDisposable
{
    private const string ContentType = "text\\plain";
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
            name: new SubscriptionName(channelName),
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
                correlationId: Guid.NewGuid().ToString(), contentType: ContentType),
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
        id.Should().NotBeNullOrEmpty();

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        awsScheduler.Should().NotBeNull();

        await Task.Delay(TimeSpan.FromMinutes(1));

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = _consumer.Receive(TimeSpan.FromMinutes(1));
            messages.Should().ContainSingle();

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                messages[0].Header.MessageType.Should().Be(MessageType.MT_COMMAND);
                messages[0].Body.Value.Should().NotBeNullOrEmpty();
                var m = JsonSerializer.Deserialize<FireAwsScheduler>(messages[0].Body.Value,
                    JsonSerialisationOptions.Options);
                m.Should().NotBeNull();
                m.SchedulerType.Should().Be(schedulerType);
                m.RequestType.Should().Be(typeof(MyCommand).FullName);
                m.Async.Should().BeFalse();
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
        awsScheduler.Should().NotBeNull();

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = _consumer.Receive(TimeSpan.FromMinutes(1));
            messages.Should().ContainSingle();

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                messages[0].Header.MessageType.Should().Be(MessageType.MT_COMMAND);
                messages[0].Body.Value.Should().NotBeNullOrEmpty();
                var m = JsonSerializer.Deserialize<FireAwsScheduler>(messages[0].Body.Value,
                    JsonSerialisationOptions.Options);
                m.Should().NotBeNull();
                m.SchedulerType.Should().Be(schedulerType);
                m.RequestType.Should().Be(typeof(MyCommand).FullName);
                m.Async.Should().BeFalse();
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
        id.Should().NotBeNullOrEmpty();

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        awsScheduler.Should().NotBeNull();

        scheduler.ReScheduler(id, TimeSpan.FromMinutes(2)).Should().BeTrue();

        var awsReScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        awsReScheduler.Should().NotBeNull();
        awsReScheduler.ScheduleExpression.Should().NotBe(awsScheduler.ScheduleExpression);

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
        id.Should().NotBeNullOrEmpty();

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        awsScheduler.Should().NotBeNull();

        scheduler.ReScheduler(id, DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(2))).Should().BeTrue();

        var awsReScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        awsReScheduler.Should().NotBeNull();
        awsReScheduler.ScheduleExpression.Should().NotBe(awsScheduler.ScheduleExpression);

        await _scheduler.DeleteScheduleAsync(new DeleteScheduleRequest { Name = id });
    }

    [Fact]
    public void When_Rescheduling_A_Sns_Request_That_Not_Exists()
    {
        var scheduler = _factory.CreateSync(null!);
        scheduler.ReScheduler(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddHours(1)).Should().BeFalse();

        scheduler.ReScheduler(Guid.NewGuid().ToString("N"), TimeSpan.FromMinutes(1)).Should().BeFalse();
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
        id.Should().NotBeNullOrEmpty();

        var awsScheduler = await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id });
        awsScheduler.Should().NotBeNull();

        scheduler.Cancel(id);

        var ex = await Catch.ExceptionAsync(async () =>
            await _scheduler.GetScheduleAsync(new GetScheduleRequest { Name = id }));

        ex.Should().NotBeNull();
        ex.Should().BeOfType<ResourceNotFoundException>();
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
