using System;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")]
    [Trait("Fragile", "CI")]
    public class SqsMessageConsumerRequeueTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannelSync _channel;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;
        private readonly MyCommand _myCommand;

        public SqsMessageConsumerRequeueTests()
        {
            _myCommand = new MyCommand{Value = "Test"};
            string correlationId = Guid.NewGuid().ToString();
            string replyTo = "http:\\queueUrl";
            string contentType = "text\\plain";
            var channelName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);
            
            SqsSubscription<MyCommand> subscription = new(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                messagePumpType: MessagePumpType.Reactor,
                routingKey: routingKey
            );
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                    replyTo: new RoutingKey(replyTo), contentType: contentType),
                new MessageBody(JsonSerializer.Serialize((object) _myCommand, JsonSerialisationOptions.Options))
            );
            
            //Must have credentials stored in the SDK Credentials store or shared credentials file
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);
            
            //We need to do this manually in a test - will create the channel from subscriber parameters
            _channelFactory = new ChannelFactory(awsConnection);
            _channel = _channelFactory.CreateSyncChannel(subscription);
            
            _messageProducer = new SqsMessageProducer(awsConnection, new SnsPublication{MakeChannels = OnMissingChannel.Create});
        }

        [Fact]
        public void When_rejecting_a_message_through_gateway_with_requeue()
        {
            _messageProducer.Send(_message);

            var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));
            
            _channel.Reject(message);

            //Let the timeout change
            Task.Delay(TimeSpan.FromMilliseconds(3000));

            //should requeue_the_message
            message = _channel.Receive(TimeSpan.FromMilliseconds(5000));
            
            //clear the queue
            _channel.Acknowledge(message);

            message.Id.Should().Be(_myCommand.Id);
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
