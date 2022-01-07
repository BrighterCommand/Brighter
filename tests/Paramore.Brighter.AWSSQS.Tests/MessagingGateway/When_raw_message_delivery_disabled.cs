using System;
using System.Collections.Generic;
using Amazon;
using Amazon.Runtime;
using FluentAssertions;
using Paramore.Brighter.AWSSQS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")] 
    [Trait("Fragile", "CI")]
    public class SqsRawMessageDeliveryTests : IDisposable
    {
        private readonly SqsMessageProducer _messageProducer;
        private readonly string _topicName; 
        private readonly ChannelFactory _channelFactory;
        private readonly IAmAChannel _channel;

        public SqsRawMessageDeliveryTests()
        {
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            _channelFactory = new ChannelFactory(awsConnection);
            var channelName = $"Raw-Msg-Delivery-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _topicName = $"Raw-Msg-Delivery-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

            var routingKey = new RoutingKey(_topicName);

            var bufferSize = 10;

            //Set rawMessageDelivery to false
            _channel = _channelFactory.CreateChannel(new SqsSubscription<MyCommand>(
                name: new SubscriptionName(channelName),
                channelName:new ChannelName(channelName),
                routingKey:routingKey,
                bufferSize: bufferSize,
                makeChannels: OnMissingChannel.Create,
                rawMessageDelivery: false));

            _messageProducer = new SqsMessageProducer(awsConnection, 
                new SnsPublication
                {
                    MakeChannels = OnMissingChannel.Create 
                });
        }

        [Fact]
        public void When_raw_message_delivery_disabled()
        {
            //arrange
            var messageHeader = new MessageHeader(Guid.NewGuid(), 
                _topicName, 
                MessageType.MT_COMMAND, 
                correlationId: Guid.NewGuid(), 
                replyTo: string.Empty, 
                contentType: "text\\plain");

            var customHeaderItem = new KeyValuePair<string, object>("custom-header-item", "custom-header-item-value");
            messageHeader.Bag.Add(customHeaderItem.Key, customHeaderItem.Value);

            var messageToSent = new Message(messageHeader, new MessageBody("test content one"));

            //act
            _messageProducer.Send(messageToSent);

            var messageReceived = _channel.Receive(10000);

            _channel.Acknowledge(messageReceived);

            //assert
            messageReceived.Id.Should().Be(messageToSent.Id);
            messageReceived.Header.Topic.Should().Be(messageToSent.Header.Topic);
            messageReceived.Header.MessageType.Should().Be(messageToSent.Header.MessageType);
            messageReceived.Header.CorrelationId.Should().Be(messageToSent.Header.CorrelationId);
            messageReceived.Header.ReplyTo.Should().Be(messageToSent.Header.ReplyTo);
            messageReceived.Header.ContentType.Should().Be(messageToSent.Header.ContentType);
            messageReceived.Header.Bag.Should().ContainKey(customHeaderItem.Key).And.ContainValue(customHeaderItem.Value);
            messageReceived.Body.Value.Should().Be(messageToSent.Body.Value);
        }

        public void Dispose()
        {
            _channelFactory.DeleteTopic();
            _channelFactory.DeleteQueue();
        }
    }
}
