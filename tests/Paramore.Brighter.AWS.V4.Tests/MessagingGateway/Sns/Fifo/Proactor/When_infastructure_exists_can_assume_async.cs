﻿using System;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Fifo.Proactor;

[Trait("Category", "AWS")]
public class AwsAssumeInfrastructureTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly SqsMessageConsumer _consumer;
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public AwsAssumeInfrastructureTestsAsync()
    {
        _myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var correlationId = Guid.NewGuid().ToString();
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var routingKey = new RoutingKey(topicName);
        var topicAttributes = new SnsAttributes { Type = SqsType.Fifo };

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            queueAttributes: new SqsAttributes(type: SqsType.Fifo),
            topicAttributes: topicAttributes,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create);

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType, partitionKey: messageGroupId),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        //We need to do this manually in a test - will create the channel from subscriber parameters
        //This doesn't look that different from our create tests - this is because we create using the channel factory in
        //our AWS transport, not the consumer (as it's a more likely to use infrastructure declared elsewhere)
        _channelFactory = new ChannelFactory(awsConnection);
        var channel = _channelFactory.CreateAsyncChannel(subscription);

        //Now change the subscription to assume that it exists 
        subscription.MakeChannels = OnMissingChannel.Assume;

        _messageProducer = new SnsMessageProducer(awsConnection,
            new SnsPublication { MakeChannels = OnMissingChannel.Assume, TopicAttributes = topicAttributes });

        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(true));
    }

    [Fact]
    public async Task When_infastructure_exists_can_assume()
    {
        //arrange
        await _messageProducer.SendAsync(_message);

        var messages = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        //Assert
        var message = messages.First();
        Assert.Equal(_myCommand.Id, message.Id);

        //clear the queue
        await _consumer.AcknowledgeAsync(message);
    }

    public void Dispose()
    {
        //Clean up resources that we have created
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
