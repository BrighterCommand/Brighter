﻿using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.MessageScheduler.Aws;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Scheduler.Events;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.Scheduler.Messages.Sns;

[Trait("Fragile", "CI")] // It isn't really fragile, it's time consumer (1-2 per test)
[Collection("Scheduler SNS")]
public class SnsSchedulingMessageViaFireSchedulerAsyncTest : IDisposable
{
    private const string ContentType = "text\\plain";
    private const int BufferSize = 3;
    private readonly SnsMessageProducer _messageProducer;
    private readonly SqsMessageConsumer _consumer;
    private readonly string _topicName;
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAMessageSchedulerFactory _factory;

    public SnsSchedulingMessageViaFireSchedulerAsyncTest()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        //we need the channel to create the queues and notifications
        _topicName = $"Producer-Fire-Scheduler-Async-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var channelName = $"Producer-Fire-Scheduler-Async-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(_topicName);

        var channel = _channelFactory.CreateAsyncChannel(new SqsSubscription<FireSchedulerMessage>(
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

        _factory = new AwsSchedulerFactory(awsConnection, "brighter-scheduler")
        {
            UseMessageTopicAsTarget = false, MakeRole = OnMissingRole.Create, SchedulerTopicOrQueue = routingKey
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
            var messages = await _consumer.ReceiveAsync();
            messages.Should().ContainSingle();

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                messages[0].Header.MessageType.Should().Be(MessageType.MT_COMMAND);
                messages[0].Body.Value.Should().NotBeNullOrEmpty();
                var m = JsonSerializer.Deserialize<FireAwsScheduler>(messages[0].Body.Value,
                    JsonSerialisationOptions.Options);
                m.Should().NotBeNull();
                m.Message.Should().BeEquivalentTo(message);
                m.Async.Should().BeTrue();
                await _consumer.AcknowledgeAsync(messages[0]);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail("The message wasn't fired");
    }

    public void Dispose()
    {
        _channelFactory.DeleteQueueAsync().GetAwaiter().GetResult();
        _channelFactory.DeleteTopicAsync().GetAwaiter().GetResult();
        _messageProducer.Dispose();
        _consumer.Dispose();
    }
}
