using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Fifo.Proactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class AWSValidateInfrastructureTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAMessageConsumerAsync _consumer;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public AWSValidateInfrastructureTestsAsync()
    {
        _myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        const string contentType = "text\\plain";
        var correlationId = Guid.NewGuid().ToString();
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var routingKey = new RoutingKey(queueName);

        var subscription = new SqsSubscription<MyCommand>(
            name: new SubscriptionName(queueName),
            channelName: new ChannelName(queueName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create,
            sqsType: SnsSqsType.Fifo,
            channelType: ChannelType.PointToPoint
        );

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType, partitionKey: messageGroupId),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var channel = _channelFactory.CreateAsyncChannel(subscription);

        subscription = new(
            name: new SubscriptionName(queueName),
            channelName: channel.Name,
            routingKey: routingKey,
            findTopicBy: TopicFindBy.Name,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Validate,
            sqsType: SnsSqsType.Fifo
        );

        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication
            {
                FindQueueBy= QueueFindBy.Name,
                MakeChannels = OnMissingChannel.Validate,
                Topic = new RoutingKey(queueName),
                SqsAttributes = new SqsAttributes { Type = SnsSqsType.Fifo }
            }
        );

        _consumer = new SqsMessageConsumerFactory(awsConnection).CreateAsync(subscription);
    }

    [Fact]
    public async Task When_infrastructure_exists_can_verify_async()
    {
        await _messageProducer.SendAsync(_message);

        await Task.Delay(1000);

        var messages = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        var message = messages.First();
        message.Id.Should().Be(_myCommand.Id);

        await _consumer.AcknowledgeAsync(message);
    }

    public void Dispose()
    {
        //Clean up resources that we have created
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        ((IAmAMessageConsumerSync)_consumer).Dispose();
        _messageProducer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _consumer.DisposeAsync();
        await _messageProducer.DisposeAsync();
    }
}
