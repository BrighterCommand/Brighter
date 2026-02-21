using System;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway
{
    [Trait("Category", "ASB")]
    [Trait("Fragile", "CI")]
    public class LargeAsbMessageProducerTests : IDisposable
    {
        private readonly IAmAChannelSync _topicChannel;
        private readonly IAmAChannelSync _queueChannel;
        private readonly IAmAProducerRegistry _producerRegistry;
        private ASBTestCommand _command;
        private readonly string _correlationId;
        private readonly ContentType _contentType;
        private readonly string _topicName;
        private readonly string _queueName;
        private readonly IAdministrationClientWrapper _administrationClient;

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

            AzureServiceBusSubscription<ASBTestCommand> subscription = new(
                subscriptionName: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey
            );

            var queueChannelName = $"Producer-queue-Send-Tests-{Guid.NewGuid()}".Truncate(50);
            _queueName = $"Producer-queue-Send-Tests-{Guid.NewGuid()}";
            var queueRoutingKey = new RoutingKey(_queueName);

            AzureServiceBusSubscription<ASBTestCommand> queueSubscription = new(
                subscriptionName: new SubscriptionName(queueChannelName),
                channelName: new ChannelName(queueChannelName),
                routingKey: queueRoutingKey,
                subscriptionConfiguration : new AzureServiceBusSubscriptionConfiguration()
                {
                    UseServiceBusQueue = true
                }
            );

            _contentType = new ContentType(MediaTypeNames.Application.Json);

            var clientProvider = ASBCreds.ASBClientProvider;
            _administrationClient = new AdministrationClientWrapper(clientProvider);
            _administrationClient.CreateQueueAsync(_queueName, TimeSpan.FromMinutes(5), 3000).GetAwaiter().GetResult();
            _administrationClient.CreateTopicAsync(_topicName, TimeSpan.FromMinutes(5), 3000).GetAwaiter().GetResult();
            _administrationClient.CreateSubscriptionAsync(_topicName, channelName, new AzureServiceBusSubscriptionConfiguration())
                .GetAwaiter()
                .GetResult();

            var channelFactory =
                new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(clientProvider));
            _topicChannel = channelFactory.CreateSyncChannel(subscription);
            _queueChannel = channelFactory.CreateSyncChannel(queueSubscription);

            _producerRegistry = new AzureServiceBusProducerRegistryFactory(
                clientProvider,
                [
                    new AzureServiceBusPublication { Topic = new RoutingKey(_topicName) },
                        new AzureServiceBusPublication { Topic = new RoutingKey(_queueName), UseServiceBusQueue = true}
                ]
                )
                .Create();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
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

            Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
            Assert.Equal(_command.Id, message.Id);
            Assert.False(message.Redelivered);
            Assert.Equal(_command.Id, message.Header.MessageId);
            Assert.Contains(testQueues ? _queueName : _topicName, message.Header.Topic.Value);
            Assert.Equal(_correlationId, message.Header.CorrelationId);
            Assert.Equal(_contentType, message.Header.ContentType);
            Assert.Equal(0, message.Header.HandledCount);
            //allow for clock drift in the following test, more important to have a contemporary timestamp than anything
            Assert.True(message.Header.TimeStamp > RoundToSeconds(DateTime.UtcNow.AddMinutes(-1)));
            Assert.Equal(TimeSpan.Zero, message.Header.Delayed);
            //{"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
            Assert.Equal(commandMessage.Body.Value, message.Body.Value);
            Assert.Contains(testHeader, message.Header.Bag.Keys);
            Assert.Equal(testHeaderValue, message.Header.Bag[testHeader]);
        }

        private Message GenerateMessage(string topicName) => new Message(
            new MessageHeader(_command.Id, new RoutingKey( topicName), MessageType.MT_COMMAND, correlationId:_correlationId,
                contentType: _contentType
            ),
            new MessageBody(JsonSerializer.Serialize(_command, JsonSerialisationOptions.Options))
        );


        public void Dispose()
        {
            _administrationClient.DeleteTopicAsync(_topicName).GetAwaiter().GetResult();
            _administrationClient.DeleteQueueAsync(_queueName).GetAwaiter().GetResult();
        }

        private DateTime RoundToSeconds(DateTime dateTime)
        {
            return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
        }
    }
}
