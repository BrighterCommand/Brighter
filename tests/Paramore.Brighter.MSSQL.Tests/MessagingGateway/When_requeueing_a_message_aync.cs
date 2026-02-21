using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway
{
    [Trait("Category", "MSSQL")]
    public class MsSqlMessageConsumerRequeueTestsAsync : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly IAmAChannelFactory _channelFactory;
        private readonly MsSqlSubscription<MyCommand> _subscription;
        private readonly RoutingKey _topic;

        public MsSqlMessageConsumerRequeueTestsAsync()
        {
            var myCommand = new MyCommand { Value = "Test" };
            string correlationId = Guid.NewGuid().ToString();
            string replyTo = "http:\\queueUrl";
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

            _subscription = new MsSqlSubscription<MyCommand>(new SubscriptionName(channelName),
                new ChannelName(_topic), new RoutingKey(_topic));
            _producerRegistry = new MsSqlProducerRegistryFactory(
                testHelper.QueueConfiguration,
                [new Publication {Topic = new RoutingKey(_topic)}]
            ).CreateAsync().Result;
            _channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration));
        }

        [Fact]
        public async Task When_requeueing_a_message_async()
        {
            await ((IAmAMessageProducerAsync)_producerRegistry.LookupAsyncBy(_topic)).SendAsync(_message);
            var channel = await _channelFactory.CreateAsyncChannelAsync(_subscription);
            var message = await channel.ReceiveAsync(TimeSpan.FromMilliseconds(2000));
            await channel.RequeueAsync(message);

            var requeuedMessage = await channel.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

            //clear the queue
            await channel.AcknowledgeAsync(requeuedMessage);

            Assert.Equal(message.Body.Value, requeuedMessage.Body.Value);
        }
        
        public void Dispose()
        {
            _producerRegistry.Dispose();
        }

    }
}
