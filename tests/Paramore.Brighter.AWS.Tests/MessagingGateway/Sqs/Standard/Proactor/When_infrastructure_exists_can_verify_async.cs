using System;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Proactor
{
    [Trait("Category", "AWS")]
    [Trait("Fragile", "CI")]
    public class AwsValidateInfrastructureTestsAsync : IDisposable, IAsyncDisposable
    {
        private readonly Message _message;
        private readonly IAmAMessageConsumerAsync _consumer;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;
        private readonly MyCommand _myCommand;

        public AwsValidateInfrastructureTestsAsync()
        {
            _myCommand = new MyCommand { Value = "Test" };
            var replyTo = new RoutingKey("http:\\queueUrl");
            var contentType = new ContentType(MediaTypeNames.Text.Plain);
            var correlationId =Id.Random();
            var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(queueName);
            var channelName = new ChannelName(queueName);
            
            var subscription = new SqsSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(subscriptionName),
                channelName: channelName,
                channelType: ChannelType.PointToPoint, 
                findQueueBy: QueueFindBy.Name,
                routingKey: routingKey, 
                messagePumpType: MessagePumpType.Proactor, 
                makeChannels: OnMissingChannel.Create);

            _message = new Message(
                new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                    replyTo: new RoutingKey(replyTo), contentType: contentType),
                new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
            );

            var awsConnection = GatewayFactory.CreateFactory();

            _channelFactory = new ChannelFactory(awsConnection);
            var channel = _channelFactory.CreateAsyncChannel(subscription);

            subscription.MakeChannels = OnMissingChannel.Validate;

            _messageProducer = new SqsMessageProducer(
                awsConnection,
                new SqsPublication(channelName: channelName, makeChannels: OnMissingChannel.Validate)
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
            Assert.Equal(_myCommand.Id, message.Id);

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
}
