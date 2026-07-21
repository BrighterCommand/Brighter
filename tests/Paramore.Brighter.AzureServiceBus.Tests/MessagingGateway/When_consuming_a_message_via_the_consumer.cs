using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway
{
    [Category("ASB")]
    public class ASBConsumerTests
    {
        private readonly Message _message;
        private IAmAChannelSync _channel;
        private IAmAProducerRegistry _producerRegistry;
        private readonly string _correlationId;
        private readonly ContentType _contentType;
        private readonly string _topicName;
        private readonly string _channelName;
        private ServiceBusClient _serviceBusClient;
        private readonly IAdministrationClientWrapper _administrationClient;
        private readonly AzureServiceBusSubscriptionConfiguration _subscriptionConfiguration;
        private readonly AzureServiceBusSubscription<ASBTestCommand> _subscription;
        private readonly IServiceBusClientProvider _clientProvider;

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

            _subscription = new AzureServiceBusSubscription<ASBTestCommand>(
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

            _clientProvider = ASBCreds.ASBClientProvider;
            _administrationClient = new AdministrationClientWrapper(_clientProvider);
        }

        [Before(Test)]
        public async Task Setup()
        {
            await _administrationClient.CreateSubscriptionAsync(_topicName, _channelName, _subscriptionConfiguration);

            _serviceBusClient = _clientProvider.GetServiceBusClient();

            var channelFactory =
                new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(_clientProvider));
            _channel = channelFactory.CreateSyncChannel(_subscription);

            _producerRegistry = await new AzureServiceBusProducerRegistryFactory(
                    _clientProvider,
                    [
                        new AzureServiceBusPublication { Topic = new RoutingKey(_topicName) }
                    ]
                )
                .CreateAsync();
        }
        
        [Test]
        public async Task When_receiving_a_message_via_the_consumer()
        {
            //arrange
            var producer = _producerRegistry.LookupBy(new RoutingKey(_topicName)) as IAmAMessageProducerAsync;

            await producer.SendAsync(_message);

            //act
            var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

            //assert
            await Assert.That(message.Id).IsEqualTo(_message.Id);
            await Assert.That(message.Header.Topic).IsEqualTo(_message.Header.Topic);
            await Assert.That(message.Header.MessageType).IsEqualTo(_message.Header.MessageType);
            await Assert.That(message.Header.Source.ToString()).IsEqualTo(_message.Header.Source.ToString());
            await Assert.That(message.Header.Type).IsEqualTo(_message.Header.Type);
            await Assert.That(message.Header.TimeStamp).IsEqualTo(_message.Header.TimeStamp).Within(TimeSpan.FromSeconds(5));
            await Assert.That(message.Header.CorrelationId).IsEqualTo(_correlationId);
            await Assert.That(message.Header.ReplyTo?.Value).IsEqualTo(_message.Header.ReplyTo?.Value);
            await Assert.That(message.Header.ContentType).IsEqualTo(_message.Header.ContentType);
            await Assert.That(message.Header.PartitionKey.Value).IsEqualTo(_message.Header.PartitionKey.Value);
            await Assert.That(message.Header.DataSchema).IsEqualTo(_message.Header.DataSchema);
            await Assert.That(message.Header.Subject).IsEqualTo(_message.Header.Subject);
            await Assert.That(message.Header.HandledCount).IsEqualTo(_message.Header.HandledCount);
            await Assert.That(message.Header.Delayed.TotalMilliseconds).IsEqualTo(_message.Header.Delayed.TotalMilliseconds) ;
            await Assert.That(message.Header.TraceParent?.Value).IsEqualTo(_message.Header.TraceParent?.Value);
            await Assert.That(message.Header.TraceState?.Value).IsEqualTo(_message.Header.TraceState?.Value);
            await Assert.That(message.Header.SpecVersion).IsEqualTo(MessageHeader.DefaultSpecVersion);
            await Assert.That(message.Header.Baggage).IsEqualTo(_message.Header.Baggage);
            
            await Assert.That(message.Body.Value).IsEqualTo(_message.Body.Value);
            
            await Assert.That(message.Redelivered).IsFalse();

            //clear the channel
            _channel.Acknowledge(message);
        }

        [Test]
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

            await Assert.That(deadLetter.MessageId).IsEqualTo(message.Id);
            await Assert.That(deadLetter.CorrelationId).IsEqualTo(_correlationId);
            await Assert.That(_message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
            await Assert.That(deadLetter.Body.ToString()).IsEqualTo(message.Body.Value);
            await Assert.That(_topicName).IsEqualTo(_message.Header.Topic.ToString());
            await Assert.That(_message.Header.Delayed).IsEqualTo(TimeSpan.Zero);
        }

        [Test]
        public async Task When_requeueing_a_message_via_the_consumer()
        {
            //arrange
            var producer = _producerRegistry.LookupBy(new RoutingKey(_topicName)) as IAmAMessageProducerAsync;

            await producer.SendAsync(_message);

            var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

            message.Header.HandledCount++;

            _channel.Requeue(message);

            var requeuedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));

            await Assert.That(requeuedMessage.Id).IsEqualTo(message.Id);
            await Assert.That(requeuedMessage.Redelivered).IsFalse();
            await Assert.That(requeuedMessage.Header.Topic).IsEqualTo(new RoutingKey(_topicName));
            await Assert.That(requeuedMessage.Header.CorrelationId).IsEqualTo(_correlationId);
            await Assert.That(requeuedMessage.Header.ContentType).IsEqualTo(_contentType);
            await Assert.That(requeuedMessage.Header.HandledCount).IsEqualTo(1);
        }

        [Test]
        public async Task When_A_Subscription_is_created_the_properties_are_set_as_Expected()
        {
            var sub = await _administrationClient.GetSubscriptionAsync(_topicName, _channelName, CancellationToken.None);

            await Assert.That(sub.DeadLetteringOnMessageExpiration).IsEqualTo(_subscriptionConfiguration.DeadLetteringOnMessageExpiration);
            await Assert.That(sub.DefaultMessageTimeToLive).IsEqualTo(_subscriptionConfiguration.DefaultMessageTimeToLive);
            await Assert.That(sub.LockDuration).IsEqualTo(_subscriptionConfiguration.LockDuration);
            await Assert.That(sub.MaxDeliveryCount).IsEqualTo(_subscriptionConfiguration.MaxDeliveryCount);

            //ToDo: Need to Add Test for Filter
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _administrationClient.DeleteTopicAsync(_topicName);
            _channel?.Dispose();
            _producerRegistry?.Dispose();
        }
    }
}
