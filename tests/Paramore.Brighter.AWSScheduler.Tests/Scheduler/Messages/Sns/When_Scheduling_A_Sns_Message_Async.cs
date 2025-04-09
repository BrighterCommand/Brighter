﻿using Paramore.Brighter.AWSScheduler.Tests.Helpers;
using Paramore.Brighter.AWSScheduler.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Aws;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWSScheduler.Tests.Scheduler.Messages.Sns;

[Trait("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 minutes per test)
[Collection("Scheduler SNS")]
public class SnsSchedulingAsyncMessageTest : IAsyncDisposable
{
    private const string ContentType = "text\\plain";
    private const int BufferSize = 3;
    private readonly SnsMessageProducer _messageProducer;
    private readonly SqsMessageConsumer _consumer;
    private readonly string _topicName;
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAMessageSchedulerFactory _factory;

    public SnsSchedulingAsyncMessageTest()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        //we need the channel to create the queues and notifications
        _topicName = $"Producer-Scheduler-Async-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var channelName = $"Producer-Scheduler-Async-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(_topicName);

        var channel = _channelFactory.CreateAsyncChannelAsync(new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            bufferSize: BufferSize,
            makeChannels: OnMissingChannel.Create
        )).GetAwaiter().GetResult();

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);
        _messageProducer = new SnsMessageProducer(awsConnection,
            new SnsPublication { MakeChannels = OnMissingChannel.Create });

        // Enforce topic to be created
        _messageProducer.SendAsync(new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: ContentType),
            new MessageBody("test content one")
        )).GetAwaiter().GetResult();

        _consumer.Purge();

        _factory = new AwsSchedulerFactory(awsConnection, "brighter-scheduler")
        {
            UseMessageTopicAsTarget = true,
            MakeRole = OnMissingRole.Create
        };
    }

    [Fact]
    public async Task When_Scheduling_A_Sns_Message_Async()
    {
        var routingKey = new RoutingKey(_topicName);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: ContentType),
            new MessageBody("test content one")
        );

        var scheduler = (IAmAMessageSchedulerAsync)_factory.Create(null!);
        await scheduler.ScheduleAsync(message, TimeSpan.FromMinutes(1));
        await Task.Delay(TimeSpan.FromMinutes(1));
        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = await _consumer.ReceiveAsync(TimeSpan.FromMinutes(1));
            Assert.Single(messages);

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                Assert.Equal((string?)message.Body.Value, (string?)messages[0].Body.Value);
                Assert.Equivalent(message.Header, messages[0].Header);
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
