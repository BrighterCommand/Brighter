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
    public class ASBProducerTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly AzureServiceBusMessageProducer _messageProducer;
        private readonly AzureServiceBusChannelFactory _channelFactory;
        private readonly ASBTestCommand _command;
        private readonly Guid _correlationId;
        private readonly string _contentType;
        private readonly string _topicName;
        private readonly IManagementClientWrapper _managementClient;

        public ASBProducerTests()
        {
            _command = new ASBTestCommand()
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

            var config = new AzureServiceBusConfiguration(ASBCreds.ASBConnectionString, false);
            
            _managementClient = new ManagementClientWrapper(config);
            _managementClient.CreateSubscription(_topicName, channelName, 5);

            _channelFactory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(config));
            _channel = _channelFactory.CreateChannel(subscription);

            _messageProducer = AzureServiceBusMessageProducerFactory.Get(config);
        }

        [Fact]
        public async Task When_posting_a_message_via_the_producer()
        {
            //arrange
            await _messageProducer.SendAsync(_message);

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
        }

        public void Dispose()
        {
            _managementClient.DeleteTopicAsync(_topicName).GetAwaiter().GetResult();
        }

        private DateTime RoundToSeconds(DateTime dateTime)
        {
            return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
        }
    }
}
