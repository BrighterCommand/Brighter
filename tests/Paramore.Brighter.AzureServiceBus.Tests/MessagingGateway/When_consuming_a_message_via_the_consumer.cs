using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Xunit;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.Observability;

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
        private readonly ContentType _contentType;
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
            
            _channelName = "test-channel";
            _topicName = $"Consumer-Tests-{Guid.NewGuid()}";
            var routingKey = new RoutingKey(_topicName);
            
            AzureServiceBusSubscription<ASBTestCommand> subscription = new(
                subscriptionName: new SubscriptionName(_channelName),
                channelName: new ChannelName(_channelName),
                routingKey: routingKey
            );

            _correlationId = Guid.NewGuid().ToString();
            _contentType = new ContentType(MediaTypeNames.Application.Json);

            var testSource = new Uri("http://testing.brightercommand.com");
            var testSchema = new Uri("http://schemas.brightercommand.com/test");
            var testPartitionKey = new PartitionKey("test-partition");
            var testReplyTo = new RoutingKey("reply-to-topic");
            var traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
            var traceState = "congo=t61rcWkgMzE";
            var baggage = new Baggage();
            baggage.LoadBaggage("userId=alice");

            _message = new Message(
                new MessageHeader(
                    messageId: command.Id,
                    topic: routingKey,
                    messageType: MessageType.MT_COMMAND,
                    source: testSource,
                    type: new CloudEventsType("goparamore.io.test.command"),
                    timeStamp: DateTimeOffset.UtcNow,
                    correlationId: _correlationId,
                    replyTo: testReplyTo,
                    contentType: _contentType,
                    partitionKey: testPartitionKey,
                    dataSchema: testSchema,
                    subject: "test-subject",
                    handledCount: 0,
                    delayed: TimeSpan.Zero,
                    traceParent: traceParent,
                    traceState: traceState,
                    baggage: baggage
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
        public async Task When_receiving_a_message_via_the_consumer()
        {
            //arrange
            var producer = _producerRegistry.LookupBy(new RoutingKey(_topicName)) as IAmAMessageProducerAsync;

            await producer.SendAsync(_message);

            //act
            var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

            //assert
            Assert.Equal(_message.Id, message.Id);
            Assert.Equal(_message.Header.Topic, message.Header.Topic);
            Assert.Equal(_message.Header.MessageType, message.Header.MessageType);
            Assert.Equal(_message.Header.Source.ToString(), message.Header.Source.ToString());
            Assert.Equal(_message.Header.Type, message.Header.Type);
            Assert.Equal(_message.Header.TimeStamp, message.Header.TimeStamp, TimeSpan.FromSeconds(5));
            Assert.Equal(_correlationId, message.Header.CorrelationId);
            Assert.Equal(_message.Header.ReplyTo?.Value, message.Header.ReplyTo?.Value);
            Assert.Equal(_message.Header.ContentType, message.Header.ContentType);
            Assert.Equal(_message.Header.PartitionKey.Value, message.Header.PartitionKey.Value);
            Assert.Equal(_message.Header.DataSchema, message.Header.DataSchema);
            Assert.Equal(_message.Header.Subject, message.Header.Subject);
            Assert.Equal(_message.Header.HandledCount, message.Header.HandledCount);
            Assert.Equal(_message.Header.Delayed.TotalMilliseconds, message.Header.Delayed.TotalMilliseconds) ;
            Assert.Equal(_message.Header.TraceParent?.Value, message.Header.TraceParent?.Value);
            Assert.Equal(_message.Header.TraceState?.Value, message.Header.TraceState?.Value);
            Assert.Equal(MessageHeader.DefaultSpecVersion, message.Header.SpecVersion);
            Assert.Equal(_message.Header.Baggage, message.Header.Baggage);
            
            Assert.Equal(_message.Body.Value, message.Body.Value);
            
            Assert.False(message.Redelivered);

            //clear the channel
            _channel.Acknowledge(message);
        }

        [Fact]
        public async Task When_rejecting_a_message_via_the_consumer()
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
            Assert.Equal(MessageType.MT_COMMAND, _message.Header.MessageType);
            Assert.Equal(message.Body.Value, deadLetter.Body.ToString());
            Assert.Equal(_message.Header.Topic.ToString(), _topicName);
            Assert.Equal(TimeSpan.Zero, _message.Header.Delayed);
        }

        [Fact]
        public async Task When_requeueing_a_message_via_the_consumer()
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
            Assert.Equal(new RoutingKey(_topicName), requeuedMessage.Header.Topic);
            Assert.Equal(_correlationId, requeuedMessage.Header.CorrelationId);
            Assert.Equal(_contentType, requeuedMessage.Header.ContentType);
            Assert.Equal(1, requeuedMessage.Header.HandledCount);
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
