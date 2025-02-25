using System;
using System.Threading;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Aws;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.Scheduler.Messages.Sns;

public class SnsSchedulingMessageTest : IDisposable
{
    private const string ContentType = "text\\plain";
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
            name: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            bufferSize: BufferSize,
            makeChannels: OnMissingChannel.Create
        ));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);
        _messageProducer = new SnsMessageProducer(awsConnection,
            new SnsPublication { MakeChannels = OnMissingChannel.Create });

        // Enforce topic to be created
        _messageProducer.Send(new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: ContentType),
            new MessageBody("test content one")
        ));

        _consumer.Purge();

        _factory = new AwsSchedulerFactory(awsConnection, "brighter-scheduler")
        {
            UseMessageTopicAsTarget = true, MakeRole = OnMissingRole.Create
        };
    }

    [Fact]
    public void When_Scheduling_A_Sns_Message()
    {
        var routingKey = new RoutingKey(_topicName);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: ContentType),
            new MessageBody("test content one")
        );

        var scheduler = (IAmAMessageSchedulerSync)_factory.Create(null!);
        scheduler.Schedule(message, TimeSpan.FromMinutes(1));

        Thread.Sleep(TimeSpan.FromMinutes(1));

        var stopAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (stopAt > DateTimeOffset.UtcNow)
        {
            var messages = _consumer.Receive();
            messages.Should().ContainSingle();

            if (messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                messages[0].Body.Value.Should().Be(message.Body.Value);
                messages[0].Header.Should().BeEquivalentTo(message.Header);
                _consumer.Acknowledge(messages[0]);
                return;
            }

            Thread.Sleep(TimeSpan.FromMinutes(1));
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
