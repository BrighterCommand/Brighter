﻿using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Standard.Proactor
{
    [Trait("Category", "AWS")]
    public class AwsValidateInfrastructureTestsAsync : IDisposable, IAsyncDisposable
    {
        private readonly Message _message;
        private readonly IAmAMessageConsumerAsync _consumer;
        private readonly SnsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;
        private readonly MyCommand _myCommand;

        public AwsValidateInfrastructureTestsAsync()
        {
            _myCommand = new MyCommand { Value = "Test" };
            string correlationId = Guid.NewGuid().ToString();
            string replyTo = "http:\\queueUrl";
            string contentType = "text\\plain";
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);

            SqsSubscription<MyCommand> subscription = new(
                subscriptionName: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                channelType: ChannelType.PubSub,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: OnMissingChannel.Create
            );

            _message = new Message(
                new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                    replyTo: new RoutingKey(replyTo), contentType: contentType),
                new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
            );

            var awsConnection = GatewayFactory.CreateFactory();

            _channelFactory = new ChannelFactory(awsConnection);
            var channel = _channelFactory.CreateAsyncChannel(subscription);

            //Now change the subscription to validate, just check what we made
            subscription.MakeChannels = OnMissingChannel.Validate; 

            _messageProducer = new SnsMessageProducer(
                awsConnection,
                new SnsPublication
                {
                    FindTopicBy = TopicFindBy.Name,
                    MakeChannels = OnMissingChannel.Validate,
                    Topic = new RoutingKey(topicName)
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
