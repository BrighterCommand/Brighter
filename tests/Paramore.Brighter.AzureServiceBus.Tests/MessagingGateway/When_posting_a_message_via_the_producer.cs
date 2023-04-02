using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway
{
    [Trait("Category", "ASB")]
    [Trait("Fragile", "CI")]
    public class ASBProducerTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly ASBTestCommand _command;
        private readonly Guid _correlationId;
        private readonly string _contentType;
        private readonly string _topicName;
        private readonly IAdministrationClientWrapper _administrationClient;

        public ASBProducerTests()
        {
            _command = new ASBTestCommand
            {
                CommandValue = "Do the things.",
                CommandNumber = 26
            };

            _correlationId = Guid.NewGuid();
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid()}".Truncate(50);
            _topicName = $"Producer-Send-Tests-{Guid.NewGuid()}";
            var routingKey = new RoutingKey(_topicName);

            AzureServiceBusSubscription<ASBTestCommand> subscription = new(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey
            );

            _contentType = "application/json";

            _message = new Message(
                new MessageHeader(_command.Id, _topicName, MessageType.MT_COMMAND, _correlationId, contentType: _contentType),
                new MessageBody(JsonSerializer.Serialize(_command, JsonSerialisationOptions.Options))
            );

            var clientProvider = ASBCreds.ASBClientProvider;
            _administrationClient = new AdministrationClientWrapper(clientProvider);
            _administrationClient.CreateSubscription(_topicName, channelName, new AzureServiceBusSubscriptionConfiguration());

            var channelFactory =
                new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(clientProvider, false));
            _channel = channelFactory.CreateChannel(subscription);

            _producerRegistry = new AzureServiceBusProducerRegistryFactory(
                clientProvider,
                new AzureServiceBusPublication[]
                    {
                        new AzureServiceBusPublication { Topic = new RoutingKey(_topicName) }
                    }
                )
                .Create();
        }

        [Fact]
        public async Task When_posting_a_message_via_the_producer()
        {
            //arrange
            string testHeader = "TestHeader";
            string testHeaderValue = "Blah!!!";
            _message.Header.Bag.Add(testHeader, testHeaderValue);
            
            var producer = _producerRegistry.LookupBy(_topicName) as IAmAMessageProducerAsync;
           
            await producer.SendAsync(_message);

            var message = _channel.Receive(5000);

            //clear the queue
            _channel.Acknowledge(message);

            message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);

            message.Id.Should().Be(_command.Id);
            message.Redelivered.Should().BeFalse();
            message.Header.Id.Should().Be(_command.Id);
            message.Header.Topic.Should().Contain(_topicName);
            message.Header.CorrelationId.Should().Be(_correlationId);
            message.Header.ContentType.Should().Be(_contentType);
            message.Header.HandledCount.Should().Be(0);
            //allow for clock drift in the following test, more important to have a contemporary timestamp than anything
            message.Header.TimeStamp.Should().BeAfter(RoundToSeconds(DateTime.UtcNow.AddMinutes(-1)));
            message.Header.DelayedMilliseconds.Should().Be(0);
            //{"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
            message.Body.Value.Should().Be(_message.Body.Value);
            message.Header.Bag.Should().Contain(testHeader, testHeaderValue);
        }

        public void Dispose()
        {
            _administrationClient.DeleteTopicAsync(_topicName).GetAwaiter().GetResult();
        }

        private DateTime RoundToSeconds(DateTime dateTime)
        {
            return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
        }
    }
}
