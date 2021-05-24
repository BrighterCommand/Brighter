﻿using System;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using FluentAssertions;
using Paramore.Brighter.AWSSQS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")]
    public class SqsMessageProducerRequeueTests : IDisposable
    {
        private readonly IAmAMessageProducer _sender;
        private Message _requeuedMessage;
        private Message _receivedMessage;
        private readonly IAmAChannel _channel;
        private readonly ChannelFactory _channelFactory;
        private readonly Message _message;

        public SqsMessageProducerRequeueTests()
        {
            MyCommand myCommand = new MyCommand{Value = "Test"};
            Guid correlationId = Guid.NewGuid();
            string replyTo = "http:\\queueUrl";
            string contentType = "text\\plain";
            var channelName = $"Producer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Producer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);
            
            var subscription = new SqsSubscription<MyCommand>(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey
            );
            
            _message = new Message(
                new MessageHeader(myCommand.Id, topicName, MessageType.MT_COMMAND, correlationId, replyTo, contentType),
                new MessageBody(JsonSerializer.Serialize((object) myCommand, JsonSerialisationOptions.Options))
            );
 
            //Must have credentials stored in the SDK Credentials store or shared credentials file
            new CredentialProfileStoreChain();
            
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);
            
            _sender = new SqsMessageProducer(awsConnection, new SqsPublication{MakeChannels = OnMissingChannel.Create});
            
            //We need to do this manually in a test - will create the channel from subscriber parameters
            _channelFactory = new ChannelFactory(awsConnection);
            _channel = _channelFactory.CreateChannel(subscription);
        }

        [Fact]
        public void When_requeueing_a_message()
        {
            _sender.Send(_message);
            _receivedMessage = _channel.Receive(5000); 
            _channel.Requeue(_receivedMessage);

            _requeuedMessage = _channel.Receive(5000);
            
            //clear the queue
            _channel.Acknowledge(_requeuedMessage );

            _requeuedMessage.Body.Value.Should().Be(_receivedMessage.Body.Value);
        }

        public void Dispose()
        {
            _channelFactory.DeleteTopic();
            _channelFactory.DeleteQueue();
        }
    }
}
