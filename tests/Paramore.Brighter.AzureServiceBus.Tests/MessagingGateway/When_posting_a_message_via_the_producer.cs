﻿using System;
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
        private readonly IAmAChannel _topicChannel;
        private readonly IAmAChannel _queueChannel;
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly ASBTestCommand _command;
        private readonly Guid _correlationId;
        private readonly string _contentType;
        private readonly string _topicName;
        private readonly string _queueName;
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
            
            var queueChannelName = $"Producer-queue-Send-Tests-{Guid.NewGuid()}".Truncate(50);
            _queueName = $"Producer-queue-Send-Tests-{Guid.NewGuid()}";
            var queueRoutingKey = new RoutingKey(_queueName);
            
            AzureServiceBusSubscription<ASBTestCommand> queueSubscription = new(
                name: new SubscriptionName(queueChannelName),
                channelName: new ChannelName(queueChannelName),
                routingKey: queueRoutingKey,
                subscriptionConfiguration : new AzureServiceBusSubscriptionConfiguration()
                {
                    UseServiceBusQueue = true
                }
            );
            

            _contentType = "application/json";

            var clientProvider = ASBCreds.ASBClientProvider;
            _administrationClient = new AdministrationClientWrapper(clientProvider);
            _administrationClient.CreateSubscription(_topicName, channelName, new AzureServiceBusSubscriptionConfiguration());

            var channelFactory =
                new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(clientProvider, false));
            _topicChannel = channelFactory.CreateChannel(subscription);
            _queueChannel = channelFactory.CreateChannel(queueSubscription);

            _producerRegistry = new AzureServiceBusProducerRegistryFactory(
                clientProvider,
                new AzureServiceBusPublication[]
                    {
                        new AzureServiceBusPublication { Topic = new RoutingKey(_topicName) },
                        new AzureServiceBusPublication { Topic = new RoutingKey(_queueName), UseServiceBusQueue = true}
                    }
                )
                .Create();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task When_posting_a_message_via_the_producer(bool testQueues)
        {
            //arrange
            string testHeader = "TestHeader";
            string testHeaderValue = "Blah!!!";
            var commandMessage = GenerateMessage(testQueues ? _queueName : _topicName);
            commandMessage.Header.Bag.Add(testHeader, testHeaderValue);
            
            var producer = _producerRegistry.LookupBy(testQueues ? _queueName : _topicName) as IAmAMessageProducerAsync;
           
            await producer.SendAsync(commandMessage);

            var channel = testQueues ? _queueChannel : _topicChannel;
            
            var message = channel.Receive(5000);

            //clear the queue
            channel.Acknowledge(message);

            message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);

            message.Id.Should().Be(_command.Id);
            message.Redelivered.Should().BeFalse();
            message.Header.Id.Should().Be(_command.Id);
            message.Header.Topic.Should().Contain(testQueues ? _queueName : _topicName);
            message.Header.CorrelationId.Should().Be(_correlationId);
            message.Header.ContentType.Should().Be(_contentType);
            message.Header.HandledCount.Should().Be(0);
            //allow for clock drift in the following test, more important to have a contemporary timestamp than anything
            message.Header.TimeStamp.Should().BeAfter(RoundToSeconds(DateTime.UtcNow.AddMinutes(-1)));
            message.Header.DelayedMilliseconds.Should().Be(0);
            //{"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
            message.Body.Value.Should().Be(commandMessage.Body.Value);
            message.Header.Bag.Should().Contain(testHeader, testHeaderValue);
        }
        
        private Message GenerateMessage(string topicName) => new Message(
            new MessageHeader(_command.Id, topicName, MessageType.MT_COMMAND, correlationId:_correlationId, 
                contentType: _contentType
            ),
            new MessageBody(JsonSerializer.Serialize(_command, JsonSerialisationOptions.Options))
        );

        public void Dispose()
        {
            _administrationClient.DeleteChannelAsync(_topicName, false).GetAwaiter().GetResult();
            _administrationClient.DeleteChannelAsync(_queueName, true).GetAwaiter().GetResult();
        }

        private DateTime RoundToSeconds(DateTime dateTime)
        {
            return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
        }
    }
}
