using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway
{
    [Trait("Category", "ASB")]
    [Trait("Fragile", "CI")]
    public class ASBConsumerTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly AzureServiceBusMessageProducer _messageProducer;
        private readonly Guid _correlationId;
        private readonly string _contentType;
        private readonly string _topicName;
        private readonly string _channelName;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly IAdministrationClientWrapper _administrationClient;

        public ASBConsumerTests()
        {
            var command = new ASBTestCommand()
            {
                CommandValue = "Do the things.",
                CommandNumber = 26
            };

            _correlationId = Guid.NewGuid();
            _channelName = $"Consumer-Tests-{Guid.NewGuid()}".Truncate(50);
            _topicName = $"Consumer-Tests-{Guid.NewGuid()}";
            var routingKey = new RoutingKey(_topicName);

            AzureServiceBusSubscription<ASBTestCommand> subscription = new(
                name: new SubscriptionName(_channelName),
                channelName: new ChannelName(_channelName),
                routingKey: routingKey
            );

            _contentType = "application/json";

            _message = new Message(
                new MessageHeader(command.Id, _topicName, MessageType.MT_COMMAND, _correlationId, contentType: _contentType),
                new MessageBody(JsonSerializer.Serialize(command, JsonSerialisationOptions.Options))
            );

            var clientProvider = ASBCreds.ASBClientProvider;
            _administrationClient = new AdministrationClientWrapper(clientProvider);
            _administrationClient.CreateSubscription(_topicName, _channelName, 5);

            _serviceBusClient = clientProvider.GetServiceBusClient();

            var channelFactory =
                new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(clientProvider, false));
            _channel = channelFactory.CreateChannel(subscription);

            
            _messageProducer = AzureServiceBusMessageProducerFactory.Get(clientProvider);
        }

        [Fact]
        public async Task When_Rejecting_a_message_via_the_consumer()
        {
            //arrange
            var deadLetterReceiver = _serviceBusClient.CreateReceiver(_topicName, _channelName, new ServiceBusReceiverOptions(){
                SubQueue = SubQueue.DeadLetter
            });
            await _messageProducer.SendAsync(_message);
    
            var message = _channel.Receive(5000);
            
            _channel.Reject(message);

            var deadLetter = await deadLetterReceiver.ReceiveMessageAsync();

            deadLetter.MessageId.Should().Be(message.Id.ToString());
            deadLetter.CorrelationId.Should().Be(_correlationId.ToString());
            deadLetter.ContentType.Should().Be(_contentType);
            deadLetter.ApplicationProperties["HandledCount"].Should().Be(0);
            //allow for clock drift in the following test, more important to have a contemporary timestamp than anything
            deadLetter.Body.ToString().Should().Be(message.Body.Value);
        }
        
        [Fact]
        public async Task When_Requeueing_a_message_via_the_consumer()
        {
            //arrange
            await _messageProducer.SendAsync(_message);

            var message = _channel.Receive(5000);

            message.Header.HandledCount++;
            
            _channel.Requeue(message);

            var requeuedMessage = _channel.Receive(5000);
            
            requeuedMessage.Id.Should().Be(message.Id);
            requeuedMessage.Redelivered.Should().BeFalse();
            requeuedMessage.Header.Id.Should().Be(message.Id);
            requeuedMessage.Header.Topic.Should().Contain(_topicName);
            requeuedMessage.Header.CorrelationId.Should().Be(_correlationId);
            requeuedMessage.Header.ContentType.Should().Be(_contentType);
            requeuedMessage.Header.HandledCount.Should().Be(1);
            //allow for clock drift in the following test, more important to have a contemporary timestamp than anything
            requeuedMessage.Header.DelayedMilliseconds.Should().Be(0);
            //{"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
            requeuedMessage.Body.Value.Should().Be(message.Body.Value);
        }

        public void Dispose()
        {
            _administrationClient.DeleteTopicAsync(_topicName).GetAwaiter().GetResult();
            _channel?.Dispose();
            _messageProducer?.Dispose();
        }
    }
}
