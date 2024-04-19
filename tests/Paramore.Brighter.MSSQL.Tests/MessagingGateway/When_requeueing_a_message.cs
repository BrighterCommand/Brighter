using System;
using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway
{
    [Trait("Category", "MSSQL")]
    public class MsSqlMessageConsumerRequeueTests
    {
        private readonly Message _message;
        private readonly IAmAProducerRegistry _producerRegistry; 
        private readonly IAmAChannelFactory _channelFactory;
        private readonly MsSqlSubscription<MyCommand> _subscription;
        private readonly string _topic;

        public MsSqlMessageConsumerRequeueTests()
        {
            var myCommand = new MyCommand { Value = "Test" };
            string correlationId = Guid.NewGuid().ToString();
            string replyTo = "http:\\queueUrl";
            string contentType = "text\\plain";
            var channelName = $"Consumer-Requeue-Tests-{Guid.NewGuid()}";
            _topic = $"Consumer-Requeue-Tests-{Guid.NewGuid()}";

            _message = new Message(
                new MessageHeader(myCommand.Id, _topic, MessageType.MT_COMMAND, correlationId, replyTo, contentType),
                new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
            );

            var testHelper = new MsSqlTestHelper();
            testHelper.SetupQueueDb();

            _subscription = new MsSqlSubscription<MyCommand>(new SubscriptionName(channelName),
                new ChannelName(_topic), new RoutingKey(_topic));
            _producerRegistry = new MsSqlProducerRegistryFactory(
                testHelper.QueueConfiguration, 
                new Publication[] {new Publication {Topic = new RoutingKey(_topic)}}
            ).Create();
            _channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration));
        }

        [Fact]
        public void When_requeueing_a_message()
        {
            ((IAmAMessageProducerSync)_producerRegistry.LookupBy(_topic)).Send(_message);
            var channel = _channelFactory.CreateChannel(_subscription);
            var message = channel.Receive(2000);
            channel.Requeue(message, 100);

            var requeuedMessage = channel.Receive(1000);

            //clear the queue
            channel.Acknowledge(requeuedMessage);

            requeuedMessage.Body.Value.Should().Be(message.Body.Value);
        }
    }
}
