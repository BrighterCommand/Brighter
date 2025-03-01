using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Xunit;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway
{
    [Trait("Category", "ASB")]
    [Trait("Fragile", "CI")]
    public class ASBConsumerTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannelSync _channel;
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly string _correlationId;
        private readonly string _contentType;
        private readonly string _topicName;
        private readonly string _channelName;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly IAdministrationClientWrapper _administrationClient;
        private readonly AzureServiceBusSubscriptionConfiguration _subscriptionConfiguration;

        public ASBConsumerTests()
        {
            var command = new ASBTestCommand
            {
                CommandValue = "Do the things.",
                CommandNumber = 26
            };

            _correlationId = Guid.NewGuid().ToString();
            _topicName = $"Consumer-Tests-{Guid.NewGuid()}";
            var routingKey = new RoutingKey(_topicName);

            AzureServiceBusSubscription<ASBTestCommand> subscription = new(
                name: new SubscriptionName(_channelName),
                channelName: new ChannelName(_channelName),
                routingKey: routingKey
            );

            _contentType = "application/json";

            _message = new Message(
                new MessageHeader(command.Id, new RoutingKey(_topicName), MessageType.MT_COMMAND, correlationId:_correlationId,
                    contentType: _contentType
                ),
                new MessageBody(JsonSerializer.Serialize(command, JsonSerialisationOptions.Options))
            );

            _subscriptionConfiguration = new AzureServiceBusSubscriptionConfiguration
            {
                DeadLetteringOnMessageExpiration = true,
                DefaultMessageTimeToLive = TimeSpan.FromDays(4),
                LockDuration = TimeSpan.FromMinutes(3),
                MaxDeliveryCount = 7,
                SqlFilter = "1=1"
            };

            var clientProvider = ASBCreds.ASBClientProvider;
            _administrationClient = new AdministrationClientWrapper(clientProvider);
            _administrationClient.CreateSubscriptionAsync(_topicName, _channelName, _subscriptionConfiguration)
                .GetAwaiter()
                .GetResult();

            _serviceBusClient = clientProvider.GetServiceBusClient();

            var channelFactory =
                new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(clientProvider));
            _channel = channelFactory.CreateSyncChannel(subscription);

            _producerRegistry = new AzureServiceBusProducerRegistryFactory(
                clientProvider,
                new[]
                    {
                        new AzureServiceBusPublication { Topic = new RoutingKey(_topicName) }
                    }
                )
                .Create();
        }

        [Fact]
        public async Task When_Rejecting_a_message_via_the_consumer()
        {
            //arrange
            var deadLetterReceiver = _serviceBusClient.CreateReceiver(_topicName, _channelName, new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter
            });

            var producer = _producerRegistry.LookupBy(new RoutingKey(_topicName)) as IAmAMessageProducerAsync;

            await producer.SendAsync(_message);

            var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

            _channel.Reject(message);

            var deadLetter = await deadLetterReceiver.ReceiveMessageAsync();

            Assert.Equal(message.Id, deadLetter.MessageId);
            Assert.Equal(_correlationId, deadLetter.CorrelationId);
            Assert.Equal(_contentType, deadLetter.ContentType);
            Assert.Equal(0, deadLetter.ApplicationProperties["HandledCount"]);
            Assert.Equal(message.Body.Value, deadLetter.Body.ToString());
        }

        [Fact]
        public async Task When_Requeueing_a_message_via_the_consumer()
        {
            //arrange
            var producer = _producerRegistry.LookupBy(new RoutingKey(_topicName)) as IAmAMessageProducerAsync;

            await producer.SendAsync(_message);

            var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

            message.Header.HandledCount++;

            _channel.Requeue(message);

            var requeuedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));

            Assert.Equal(message.Id, requeuedMessage.Id);
            Assert.False(requeuedMessage.Redelivered);
            Assert.Equal(message.Id, requeuedMessage.Header.MessageId);
            Assert.Equal(new RoutingKey(_topicName), requeuedMessage.Header.Topic);
            Assert.Equal(_correlationId, requeuedMessage.Header.CorrelationId);
            Assert.Equal(_contentType, requeuedMessage.Header.ContentType);
            Assert.Equal(1, requeuedMessage.Header.HandledCount);
            Assert.Equal(TimeSpan.Zero, requeuedMessage.Header.Delayed);
            Assert.Equal(message.Body.Value, requeuedMessage.Body.Value);
        }

        [Fact]
        public async Task When_A_Subscription_is_created_the_properties_are_set_as_Expected()
        {
            var sub = await _administrationClient.GetSubscriptionAsync(_topicName, _channelName, CancellationToken.None);

            Assert.Equal(_subscriptionConfiguration.DeadLetteringOnMessageExpiration, sub.DeadLetteringOnMessageExpiration);
            Assert.Equal(_subscriptionConfiguration.DefaultMessageTimeToLive, sub.DefaultMessageTimeToLive);
            Assert.Equal(_subscriptionConfiguration.LockDuration, sub.LockDuration);
            Assert.Equal(_subscriptionConfiguration.MaxDeliveryCount, sub.MaxDeliveryCount);

            //ToDo: Need to Add Test for Filter
        }

        public void Dispose()
        {
            _administrationClient.DeleteTopicAsync(_topicName).GetAwaiter().GetResult();
            _channel?.Dispose();
            _producerRegistry?.Dispose();
        }
    }
}
