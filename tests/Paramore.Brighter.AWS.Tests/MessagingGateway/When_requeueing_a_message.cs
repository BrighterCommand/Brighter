using System;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")]
    public class SqsMessageProducerRequeueTests : IDisposable, IAsyncDisposable
    {
        private readonly IAmAMessageProducerSync _sender;
        private Message _requeuedMessage;
        private Message _receivedMessage;
        private readonly IAmAChannelSync _channel;
        private readonly ChannelFactory _channelFactory;
        private readonly Message _message;

        public SqsMessageProducerRequeueTests()
        {
            MyCommand myCommand = new MyCommand{Value = "Test"};
            string correlationId = Guid.NewGuid().ToString();
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
                new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId, 
                    replyTo: new RoutingKey(replyTo), contentType: contentType),
                new MessageBody(JsonSerializer.Serialize((object) myCommand, JsonSerialisationOptions.Options))
            );
 
            //Must have credentials stored in the SDK Credentials store or shared credentials file
            new CredentialProfileStoreChain();
            
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);
            
            _sender = new SqsMessageProducer(awsConnection, new SnsPublication{MakeChannels = OnMissingChannel.Create});
            
            //We need to do this manually in a test - will create the channel from subscriber parameters
            _channelFactory = new ChannelFactory(awsConnection);
            _channel = _channelFactory.CreateSyncChannel(subscription);
        }

        [Fact]
        public void When_requeueing_a_message()
        {
            _sender.Send(_message);
            _receivedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000)); 
            _channel.Requeue(_receivedMessage);

            _requeuedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
            
            //clear the queue
            _channel.Acknowledge(_requeuedMessage );

            _requeuedMessage.Body.Value.Should().Be(_receivedMessage.Body.Value);
        }

        public void Dispose()
        {
            _channelFactory.DeleteTopicAsync().Wait(); 
            _channelFactory.DeleteQueueAsync().Wait();
        }
        
        public async ValueTask DisposeAsync()
        {
            await _channelFactory.DeleteTopicAsync(); 
            await _channelFactory.DeleteQueueAsync();
        }
    }
}
