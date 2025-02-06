using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Transactions;
using System.Xml;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Aws;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessageScheduler;

public class SnsSchedulingMessageTest
{
    private const string ContentType = "text\\plain";
    private readonly SnsMessageProducer _messageProducer;
    private readonly SqsMessageConsumer _consumer;
    private readonly string _topicName;
    private readonly ChannelFactory _channelFactory;
    private readonly AwsMessageSchedulerFactory _factory;
    

    public SnsSchedulingMessageTest()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var channelName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _topicName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var routingKey = new RoutingKey(_topicName);

        var channel = _channelFactory.CreateSyncChannel(new SqsSubscription<MyCommand>(
            name: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            makeChannels: OnMissingChannel.Create
        ));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName());
        _messageProducer = new SnsMessageProducer(awsConnection, new SnsPublication { MakeChannels = OnMissingChannel.Create });

        var role = Role
            .GetOrCreateRoleAsync(new AWSClientFactory(awsConnection), "test-scheduler")
            .GetAwaiter()
            .GetResult();
        _factory = new AwsMessageSchedulerFactory(awsConnection, role, new RoutingKey("scheduler"))
        {
            UseMessageTopicAsTarget = true
        };
    }

    [Fact]
    public void Test()
    {
        var routingKey = new RoutingKey(_topicName);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND, 
                correlationId: Guid.NewGuid().ToString(), contentType: ContentType),
            new MessageBody("test content one")
        );

        var scheduler = (IAmAMessageSchedulerAsync)_factory.Create(null!)!;
        scheduler.ScheduleAsync(message, DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(1)));
        var messages = _consumer.Receive(TimeSpan.FromSeconds(2));
        messages.Should().ContainSingle();
        messages[0].Body.Value.Should().Be(message.Body.Value);
        messages[0].Header.Should().BeEquivalentTo(message.Header);
    }

    internal class EmptyHandlerFactorySync : IAmAHandlerFactorySync
    {
        public IHandleRequests Create(Type handlerType, IAmALifetime lifetime)
        {
            return null;
        }

        public void Release(IHandleRequests handler, IAmALifetime lifetime) { }
    }
}
