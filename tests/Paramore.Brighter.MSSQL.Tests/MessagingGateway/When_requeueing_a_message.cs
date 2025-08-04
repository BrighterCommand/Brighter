using System;
using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
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
        private readonly RoutingKey _topic;

        public MsSqlMessageConsumerRequeueTests()
        {
            var myCommand = new MyCommand { Value = "Test" };
            var correlationId = Id.Random();
            var replyTo = new RoutingKey("http:\\queueUrl");
            var contentType = new ContentType(MediaTypeNames.Text.Plain);
            var channelName = $"Consumer-Requeue-Tests-{Guid.NewGuid()}";
            _topic = new RoutingKey($"Consumer-Requeue-Tests-{Guid.NewGuid()}");

            _message = new Message(
                new MessageHeader(myCommand.Id, _topic, MessageType.MT_COMMAND, correlationId:correlationId, 
                    replyTo:new RoutingKey(replyTo), contentType:contentType),
                new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
            );

            var testHelper = new MsSqlTestHelper();
            testHelper.SetupQueueDb();

            _subscription = new MsSqlSubscription<MyCommand>(
                new SubscriptionName(channelName),
                new ChannelName(_topic), new RoutingKey(_topic),
                messagePumpType: MessagePumpType.Reactor);
            
            _producerRegistry = new MsSqlProducerRegistryFactory(
                testHelper.QueueConfiguration,
                [new Publication {Topic = new RoutingKey(_topic)}]
            ).Create();
            _channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration));
        }

        [Fact]
        public void When_requeueing_a_message()
        {
            ((IAmAMessageProducerSync)_producerRegistry.LookupBy(_topic)).Send(_message);
            var channel = _channelFactory.CreateSyncChannel(_subscription);
            var message = channel.Receive(TimeSpan.FromMilliseconds(2000));
            channel.Requeue(message, TimeSpan.FromMilliseconds(100));

            var requeuedMessage = channel.Receive(TimeSpan.FromMilliseconds(1000));

            //clear the queue
            channel.Acknowledge(requeuedMessage);

            Assert.Equal(message.Body.Value, requeuedMessage.Body.Value);
        }
    }
}
