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
    public class CustomisingAwsClientConfigTestsAsync : IDisposable, IAsyncDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannelAsync _channel;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;

        private readonly InterceptingDelegatingHandler _publishHttpHandler = new();
        private readonly InterceptingDelegatingHandler _subscribeHttpHandler = new();

        public CustomisingAwsClientConfigTestsAsync()
        {
            MyCommand myCommand = new() {Value = "Test"};
            string correlationId = Guid.NewGuid().ToString();
            string replyTo = "http:\\queueUrl";
            string contentType = "text\\plain";
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);
            
            SqsSubscription<MyCommand> subscription = new(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                messagePumpType: MessagePumpType.Proactor,
                routingKey: routingKey
            );
            
            _message = new Message(
                new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                    replyTo: new RoutingKey(replyTo), contentType: contentType),
                new MessageBody(JsonSerializer.Serialize((object) myCommand, JsonSerialisationOptions.Options))
            );

            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var subscribeAwsConnection = new AWSMessagingGatewayConnection(credentials, region, config =>
            {
                config.HttpClientFactory = new InterceptingHttpClientFactory(_subscribeHttpHandler);
            });
            
            _channelFactory = new ChannelFactory(subscribeAwsConnection);
            _channel = _channelFactory.CreateAsyncChannel(subscription);

            var publishAwsConnection = new AWSMessagingGatewayConnection(credentials, region, config =>
            {
                config.HttpClientFactory = new InterceptingHttpClientFactory(_publishHttpHandler);
            });

            _messageProducer = new SqsMessageProducer(publishAwsConnection, new SnsPublication{Topic = new RoutingKey(topicName), MakeChannels = OnMissingChannel.Create});
        }

        [Fact]
        public async Task When_customising_aws_client_config()
        {
            //arrange
            await _messageProducer.SendAsync(_message);
            
            await Task.Delay(1000);
            
            var message =await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));
            
            //clear the queue
            await _channel.AcknowledgeAsync(message);

            //publish_and_subscribe_should_use_custom_http_client_factory
            _publishHttpHandler.RequestCount.Should().BeGreaterThan(0);
            _subscribeHttpHandler.RequestCount.Should().BeGreaterThan(0);
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
}
