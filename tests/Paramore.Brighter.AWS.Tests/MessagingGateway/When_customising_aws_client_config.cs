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
    public class CustomisingAwsClientConfigTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;
        private readonly MyCommand _myCommand;
        private readonly Guid _correlationId;
        private readonly string _replyTo;
        private readonly string _contentType;
        private readonly string _topicName;

        private readonly InterceptingDelegatingHandler _publishHttpHandler;
        private readonly InterceptingDelegatingHandler _subscribeHttpHandler;

        public CustomisingAwsClientConfigTests()
        {
            _myCommand = new MyCommand{Value = "Test"};
            _correlationId = Guid.NewGuid();
            _replyTo = "http:\\queueUrl";
            _contentType = "text\\plain";
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(_topicName);
            
            SqsSubscription<MyCommand> subscription = new(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey
            );
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, _topicName, MessageType.MT_COMMAND, correlationId: _correlationId,
                    replyTo: _replyTo, contentType: _contentType),
                new MessageBody(JsonSerializer.Serialize((object) _myCommand, JsonSerialisationOptions.Options))
            );

            _subscribeHttpHandler = new InterceptingDelegatingHandler();
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var subscribeAwsConnection = new AWSMessagingGatewayConnection(credentials, region, config =>
            {
                config.HttpClientFactory = new InterceptingHttpClientFactory(_subscribeHttpHandler);
            });
            
            _channelFactory = new ChannelFactory(subscribeAwsConnection);
            _channel = _channelFactory.CreateChannel(subscription);

            var publishAwsConnection = new AWSMessagingGatewayConnection(credentials, region, config =>
            {
                config.HttpClientFactory = new InterceptingHttpClientFactory(_publishHttpHandler);
            });

            _messageProducer = new SqsMessageProducer(publishAwsConnection, new SnsPublication{Topic = new RoutingKey(_topicName), MakeChannels = OnMissingChannel.Create});
        }

        [Fact]
        public async Task When_customising_aws_client_config()
        {
            //arrange
            _messageProducer.Send(_message);
            
            await Task.Delay(1000);
            
            var message =_channel.Receive(5000);
            
            //clear the queue
            _channel.Acknowledge(message);

            //publish_and_subscribe_should_use_custom_http_client_factory
            _publishHttpHandler.RequestCount.Should().BeGreaterThan(0);
            _subscribeHttpHandler.RequestCount.Should().BeGreaterThan(0);
        }

        public void Dispose()
        {
            _channelFactory?.DeleteTopic();
            _channelFactory?.DeleteQueue();
            _messageProducer?.Dispose();
        }
    }
}
