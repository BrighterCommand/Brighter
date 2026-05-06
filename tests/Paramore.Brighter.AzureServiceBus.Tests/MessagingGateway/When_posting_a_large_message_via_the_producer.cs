using System;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway
{
    [Category("ASB")]
    [Property("Fragile", "CI")]
    public class LargeAsbMessageProducerTests
    {
        private IAmAChannelSync _topicChannel;
        private IAmAChannelSync _queueChannel;
        private IAmAProducerRegistry _producerRegistry;
        private ASBTestCommand _command;
        private readonly string _correlationId;
        private readonly ContentType _contentType;
        private readonly string _topicName;
        private readonly string _queueName;
        private readonly IAdministrationClientWrapper _administrationClient;
        private readonly IServiceBusClientProvider _clientProvider;
        private readonly AzureServiceBusSubscription<ASBTestCommand> _subscription;
        private readonly AzureServiceBusSubscription<ASBTestCommand> _queueSubscription;

        public LargeAsbMessageProducerTests()
        {
            _command = new ASBTestCommand
            {
                CommandValue = "Do the things.",
                CommandNumber = 26
            };

            _correlationId = Guid.NewGuid().ToString();
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid()}".Truncate(50);
            _topicName = $"Producer-Send-Tests-{Guid.NewGuid()}";
            var routingKey = new RoutingKey(_topicName);

            _subscription = new AzureServiceBusSubscription<ASBTestCommand>(
                subscriptionName: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey
            );

            var queueChannelName = $"Producer-queue-Send-Tests-{Guid.NewGuid()}".Truncate(50);
            _queueName = $"Producer-queue-Send-Tests-{Guid.NewGuid()}";
            var queueRoutingKey = new RoutingKey(_queueName);

            _queueSubscription = new AzureServiceBusSubscription<ASBTestCommand>(
                subscriptionName: new SubscriptionName(queueChannelName),
                channelName: new ChannelName(queueChannelName),
                routingKey: queueRoutingKey,
                subscriptionConfiguration : new AzureServiceBusSubscriptionConfiguration()
                {
                    UseServiceBusQueue = true
                }
            );

            _contentType = new ContentType(MediaTypeNames.Application.Json);

            _clientProvider = ASBCreds.ASBClientProvider;
            _administrationClient = new AdministrationClientWrapper(_clientProvider);
        }

        [Before(Test)]
        public async Task Setup()
        {
            await _administrationClient.CreateQueueAsync(_queueName, TimeSpan.FromMinutes(5), 3000);
            await _administrationClient.CreateTopicAsync(_topicName, TimeSpan.FromMinutes(5), 3000);
            await _administrationClient.CreateSubscriptionAsync(_topicName, _subscription.ChannelName.Value, new AzureServiceBusSubscriptionConfiguration());

            var channelFactory =
                new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(_clientProvider));
            _topicChannel = channelFactory.CreateSyncChannel(_subscription);
            _queueChannel = channelFactory.CreateSyncChannel(_queueSubscription);

            _producerRegistry = await new AzureServiceBusProducerRegistryFactory(
                    _clientProvider,
                    [
                        new AzureServiceBusPublication { Topic = new RoutingKey(_topicName) },
                        new AzureServiceBusPublication { Topic = new RoutingKey(_queueName), UseServiceBusQueue = true}
                    ]
                )
                .CreateAsync();
        }

        [Test]
        [Arguments(true)]
        [Arguments(false)]
        public async Task When_posting_a_large_message_via_the_bulk_producer(bool testQueues)
        {
            //arrange
            string testHeader = "TestHeader";
            string testHeaderValue = "Blah!!!";
            _command = ASBTestCommand.BuildLarge();
            var commandMessage = GenerateMessage(testQueues ? _queueName : _topicName);
            commandMessage.Header.Bag.Add(testHeader, testHeaderValue);

            var producer = _producerRegistry.LookupBy(testQueues
                ? new RoutingKey(_queueName) : new RoutingKey(_topicName)) as IAmABulkMessageProducerAsync;

            var batches = await producer.CreateBatchesAsync([commandMessage], CancellationToken.None);
            await producer.SendAsync(batches.Single(), CancellationToken.None);

            var channel = testQueues ? _queueChannel : _topicChannel;

            var message = channel.Receive(TimeSpan.FromMilliseconds(5000));

            //clear the queue
            channel.Acknowledge(message);

            await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
            await Assert.That(message.Id).IsEqualTo(_command.Id);
            await Assert.That(message.Redelivered).IsFalse();
            await Assert.That(message.Header.MessageId).IsEqualTo(_command.Id);
            await Assert.That(message.Header.Topic.Value).Contains(testQueues ? _queueName : _topicName);
            await Assert.That(message.Header.CorrelationId).IsEqualTo(_correlationId);
            await Assert.That(message.Header.ContentType).IsEqualTo(_contentType);
            await Assert.That(message.Header.HandledCount).IsEqualTo(0);
            //allow for clock drift in the following test, more important to have a contemporary timestamp than anything
            await Assert.That(message.Header.TimeStamp > RoundToSeconds(DateTime.UtcNow.AddMinutes(-1))).IsTrue();
            await Assert.That(message.Header.Delayed).IsEqualTo(TimeSpan.Zero);
            //{"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
            await Assert.That(message.Body.Value).IsEqualTo(commandMessage.Body.Value);
            await Assert.That(message.Header.Bag.Keys).Contains(testHeader);
            await Assert.That(message.Header.Bag[testHeader]).IsEqualTo(testHeaderValue);
        }

        private Message GenerateMessage(string topicName) => new Message(
            new MessageHeader(_command.Id, new RoutingKey( topicName), MessageType.MT_COMMAND, correlationId:_correlationId,
                contentType: _contentType
            ),
            new MessageBody(JsonSerializer.Serialize(_command, JsonSerialisationOptions.Options))
        );


        [After(Test)]
        public async Task Cleanup()
        {
            await _administrationClient.DeleteTopicAsync(_topicName);
            await _administrationClient.DeleteQueueAsync(_queueName);
        }

        private DateTime RoundToSeconds(DateTime dateTime)
        {
            return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
        }
    }
}
