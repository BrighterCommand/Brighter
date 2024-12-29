using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Fifo;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class SQSBufferedConsumerTests : IDisposable
{
    private readonly SqsMessageProducer _messageProducer;
    private readonly SqsMessageConsumer _consumer;
    private readonly string _topicName;
    private readonly ChannelFactory _channelFactory;
    private const string _contentType = "text\\plain";
    private const int _bufferSize = 3;
    private const int _messageCount = 4;

    public SQSBufferedConsumerTests()
    {
        (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
        var awsConnection = new AWSMessagingGatewayConnection(credentials, region);

        _channelFactory = new ChannelFactory(awsConnection);

        var channelName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _topicName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

        //we need the channel to create the queues and notifications
        var routingKey = new RoutingKey(_topicName);

        var channel = _channelFactory.CreateChannel(new SqsSubscription<MyCommand>(
            name: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            bufferSize: _bufferSize,
            makeChannels: OnMissingChannel.Create,
            sqsType: SnsSqsType.Fifo,
            contentBasedDeduplication: true
        ));

        //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
        //just for the tests, so create a new consumer from the properties
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), routingKey,
            _bufferSize);
        _messageProducer = new SqsMessageProducer(awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create, SnsType = SnsSqsType.Fifo, Deduplication = true
            });
    }

    [Fact]
    public async Task When_a_message_consumer_reads_multiple_messages_per_group()
    {
        var routingKey = new RoutingKey(_topicName);
        var messageGroupIdOne = "123";

        var messageOne = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                partitionKey: messageGroupIdOne),
            new MessageBody("test content one")
        );

        var messageTwo = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                partitionKey: messageGroupIdOne),
            new MessageBody("test content two")
        );

        var messageThree = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                partitionKey: messageGroupIdOne),
            new MessageBody("test content three")
        );

        var messageFour = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                partitionKey: messageGroupIdOne),
            new MessageBody("test content four")
        );
        var messageGroupIdTwo = "1234";
        var messageFive = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                partitionKey: messageGroupIdTwo),
            new MessageBody("test content five")
        );

        var messageSix = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                partitionKey: messageGroupIdTwo),
            new MessageBody("test content six")
        );

        var messageSeven = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                partitionKey: messageGroupIdTwo) { Bag = { [HeaderNames.DeduplicationId] = "123" } },
            new MessageBody("test content seven")
        );

        var messageEight = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                partitionKey: messageGroupIdTwo) { Bag = { [HeaderNames.DeduplicationId] = "123" } },
            new MessageBody("test content eight")
        );

        //send MESSAGE_COUNT messages 
        _messageProducer.Send(messageOne);
        _messageProducer.Send(messageTwo);
        _messageProducer.Send(messageThree);
        _messageProducer.Send(messageFour);
        _messageProducer.Send(messageFive);
        _messageProducer.Send(messageSix);
        _messageProducer.Send(messageSeven);
        _messageProducer.Send(messageEight);

        int iteration = 0;
        var messagesReceived = new List<Message>();
        var messagesReceivedCount = messagesReceived.Count;
        do
        {
            iteration++;

            //retrieve  messages
            var messages = _consumer.Receive(TimeSpan.FromMilliseconds(10000));

            // should not receive more number of message group
            messages.Length.Should().BeLessOrEqualTo(2);

            var moreMessages = messages.Where(m => m.Header.MessageType == MessageType.MT_COMMAND);
            foreach (var message in moreMessages)
            {
                messagesReceived.Add(message);
                _consumer.Acknowledge(message);
            }

            messagesReceivedCount = messagesReceived.Count;

            await Task.Delay(1000);
        } while ((iteration <= 7) && (messagesReceivedCount < _messageCount));


        messagesReceivedCount.Should().Be(7);
    }

    public void Dispose()
    {
        //Clean up resources that we have created
        _channelFactory.DeleteTopic();
        _channelFactory.DeleteQueue();
    }
}
