using System;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway.Proactor
{
    public class AzureServiceBusMessageProducerTestsAsync
    {
        private readonly FakeAdministrationClient _nameSpaceManagerWrapper;
        private readonly FakeServiceBusSenderProvider _topicClientProvider;
        private readonly FakeServiceBusSenderWrapper _topicClient;
        private readonly AzureServiceBusMessageProducer _producer;
        private readonly AzureServiceBusMessageProducer _queueProducer;

        public AzureServiceBusMessageProducerTestsAsync()
        {
            _nameSpaceManagerWrapper = new FakeAdministrationClient();
            _topicClient = new FakeServiceBusSenderWrapper();
            _topicClientProvider = new FakeServiceBusSenderProvider(_topicClient);
            

            _producer = new AzureServiceBusTopicMessageProducer(
                _nameSpaceManagerWrapper, 
                _topicClientProvider, 
                new AzureServiceBusPublication{MakeChannels = OnMissingChannel.Create}
            );
            
            _queueProducer = new AzureServiceBusQueueMessageProducer(
                _nameSpaceManagerWrapper, 
                _topicClientProvider, 
                new AzureServiceBusPublication{MakeChannels = OnMissingChannel.Create}
            );
        }

        [Fact]
        public async Task When_the_topic_exists_and_sending_a_message_with_no_delay_it_should_send_the_message_to_the_correct_topicclient()
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            
            await _producer.SendAsync(new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_EVENT), 
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json)))
            );
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_EVENT", sentMessage.ApplicationProperties["MessageType"]);
            Assert.Equal(0, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_sending_a_command_message_type_message_with_no_delay_it_should_set_the_correct_messagetype_property(bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            _nameSpaceManagerWrapper.Queues.Add("topic");

            var producer = useQueues ? _queueProducer : _producer;
            
            await producer.SendAsync(new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_COMMAND), 
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json)))
            );
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_COMMAND", sentMessage.ApplicationProperties["MessageType"]);
            Assert.Equal(0, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_the_topic_does_not_exist_it_should_be_created_and_the_message_is_sent_to_the_correct_topicclient(bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();

            var producer = useQueues ? _queueProducer : _producer;
            
            await producer.SendAsync(new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE), 
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))));
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(1, _nameSpaceManagerWrapper.CreateCount);
            Assert.Equal(messageBody, sentMessage.Body.ToArray());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_a_message_fails_to_send_the_sender_is_kept_for_reuse_and_closed_on_dispose(bool useQueues)
        {
            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            _nameSpaceManagerWrapper.Queues.Add("topic");

            _topicClient.SendException = new Exception("Failed");

            var producer = useQueues ? _queueProducer : _producer;

            try
            {
                await producer.SendAsync(new Message(
                    new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE), 
                    new MessageBody("Message", new ContentType(MediaTypeNames.Application.Json))));
            }
            catch (Exception)
            {
                // ignored
            }

            _topicClient.SendException = null;
            Assert.Equal(0, _topicClient.ClosedCount);
            Assert.Equal(1, _topicClientProvider.CreationCount);

            await producer.DisposeAsync();

            Assert.Equal(1, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_the_topic_exists_and_sending_a_message_with_a_delay_it_should_send_the_message_to_the_correct_topicclient(bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            _nameSpaceManagerWrapper.Queues.Add("topic");

            var producer = useQueues ? _queueProducer : _producer;
            
            await producer.SendWithDelayAsync(
                new Message(
                    new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_EVENT),
                    new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))), TimeSpan.FromSeconds(1));
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_EVENT", sentMessage.ApplicationProperties["MessageType"]);
            Assert.Equal(0, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_sending_a_command_message_type_message_with_delay_it_should_set_the_correct_messagetype_property(bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            _nameSpaceManagerWrapper.Queues.Add("topic");

            var producer = useQueues ? _queueProducer : _producer;

            await producer.SendWithDelayAsync(new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_COMMAND),
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))), TimeSpan.FromSeconds(1));
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_COMMAND", sentMessage.ApplicationProperties["MessageType"]);
            Assert.Equal(0, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_the_topic_does_not_exist_and_sending_a_message_with_a_delay_it_should_send_the_message_to_the_correct_topicclient(bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();

            var producer = useQueues ? _queueProducer : _producer;

            await producer.SendWithDelayAsync(new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE),
                new MessageBody(messageBody,new ContentType(MediaTypeNames.Application.Json))), TimeSpan.FromSeconds(1));
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(1, _nameSpaceManagerWrapper.CreateCount);
            
            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal(0, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public async Task Once_the_topic_is_created_it_then_does_not_check_if_it_exists_every_time(bool topicExists, bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();
            if (topicExists)
            {
                _nameSpaceManagerWrapper.Topics.Add("topic", []);
                _nameSpaceManagerWrapper.Queues.Add("topic");    
            }

            var producer = useQueues ? _queueProducer : _producer;

            var routingKey = new RoutingKey("topic");
            
            await producer.SendWithDelayAsync(new Message(
                new MessageHeader(Id.Random(), routingKey, MessageType.MT_NONE), 
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))), TimeSpan.FromSeconds(1));
            await producer.SendWithDelayAsync(new Message(
                new MessageHeader(Id.Random(), routingKey, MessageType.MT_NONE), 
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))), TimeSpan.FromSeconds(1));

            if (topicExists == false)
                Assert.Equal(1, _nameSpaceManagerWrapper.CreateCount);

            Assert.Equal(1, _nameSpaceManagerWrapper.ExistCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_there_is_an_error_talking_to_servicebus_when_creating_the_topic_the_ManagementClientWrapper_is_reinitilised(bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ExistsException = new Exception();

            var producer = useQueues ? _queueProducer : _producer;
            
            await Assert.ThrowsAsync<Exception>(() => producer.SendWithDelayAsync(
                new Message(
                    new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE), 
                    new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))), TimeSpan.FromSeconds(1))
            );
            Assert.Equal(1, _nameSpaceManagerWrapper.ResetCount);
        }


        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void When_there_is_an_error_getting_a_topic_client_the_connection_for_topic_client_is_retried(bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            _nameSpaceManagerWrapper.Queues.Add("topic");

            _topicClientProvider.SingleThrowGetException = new Exception();

            var producer = useQueues ? _queueProducer : _producer;
            
            producer.SendWithDelay(new Message(
               new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE), 
               new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json)))
           );

            Assert.Single(_topicClient.SentMessages);
        }

        [Fact]
        public async Task When_the_topic_does_not_exist_and_Missing_is_set_to_Validate_an_exception_is_raised()
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            var producerValidate = new AzureServiceBusTopicMessageProducer(
                _nameSpaceManagerWrapper, 
                _topicClientProvider, 
                new AzureServiceBusPublication{MakeChannels = OnMissingChannel.Validate})
            ;

            await Assert.ThrowsAsync<ChannelFailureException>(() => producerValidate.SendAsync(
                new Message(
                    new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE), 
                    new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))))
            );
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_sending_several_messages_to_one_entity_the_producer_reuses_one_sender(bool useQueues)
        {
            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            _nameSpaceManagerWrapper.Topics.Add("other-topic", []);
            _nameSpaceManagerWrapper.Queues.Add("topic");
            _nameSpaceManagerWrapper.Queues.Add("other-topic");

            var producer = useQueues ? _queueProducer : _producer;

            for (var i = 0; i < 3; i++)
                await producer.SendAsync(new Message(
                    new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_EVENT),
                    new MessageBody("Message", new ContentType(MediaTypeNames.Application.Json))));

            Assert.Equal(1, _topicClientProvider.CreationCount);

            await producer.SendAsync(new Message(
                new MessageHeader(Id.Random(), new RoutingKey("other-topic"), MessageType.MT_EVENT),
                new MessageBody("Message", new ContentType(MediaTypeNames.Application.Json))));

            Assert.Equal(4, _topicClient.SentMessages.Count);
            Assert.Equal(2, _topicClientProvider.CreationCount);
            Assert.Equal(0, _topicClient.ClosedCount);
        }

        [Fact]
        public async Task When_closing_a_cached_sender_fails_on_dispose_the_other_senders_still_close()
        {
            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            _nameSpaceManagerWrapper.Topics.Add("other-topic", []);

            var failingSender = new FakeServiceBusSenderWrapper { CloseException = new Exception("close failed") };
            var healthySender = new FakeServiceBusSenderWrapper();
            _topicClientProvider.SenderFactory = topic => topic == "topic" ? failingSender : healthySender;

            await _producer.SendAsync(CreateMessage("topic"));
            await _producer.SendAsync(CreateMessage("other-topic"));

            await _producer.DisposeAsync();

            Assert.Equal(1, failingSender.CloseAttempts);
            Assert.Equal(0, failingSender.ClosedCount);
            Assert.Equal(1, healthySender.ClosedCount);

            await _producer.DisposeAsync();

            Assert.Equal(1, failingSender.CloseAttempts);
            Assert.Equal(1, healthySender.ClosedCount);
        }

        [Fact]
        public async Task When_sending_after_the_producer_is_disposed_it_throws_and_caches_nothing()
        {
            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);

            await _producer.SendAsync(CreateMessage("topic"));

            await _producer.DisposeAsync();

            Assert.Equal(1, _topicClient.ClosedCount);
            Assert.Equal(1, _topicClientProvider.CreationCount);

            await Assert.ThrowsAsync<ObjectDisposedException>(() => _producer.SendAsync(CreateMessage("topic")));

            Assert.Equal(1, _topicClientProvider.CreationCount);
            Assert.Equal(1, _topicClient.ClosedCount);
        }

        [Fact]
        public async Task When_the_sender_created_by_the_losing_caller_fails_to_close_the_send_still_succeeds()
        {
            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);

            var cachedSender = new FakeServiceBusSenderWrapper();
            var loserSender = new FakeServiceBusSenderWrapper { CloseException = new Exception("close failed") };
            var created = 0;

            // While the first caller sits between its cache miss and its GetOrAdd, run a second send to the
            // same entity to completion, so the second sender is the one cached and the first must lose.
            _topicClientProvider.SenderFactory = _ =>
            {
                if (++created == 1)
                {
                    _producer.SendAsync(CreateMessage("topic")).GetAwaiter().GetResult();
                    return loserSender;
                }

                return cachedSender;
            };

            await _producer.SendAsync(CreateMessage("topic"));

            Assert.Equal(2, created);
            Assert.Equal(1, loserSender.CloseAttempts);
            Assert.Equal(0, loserSender.ClosedCount);
            Assert.Equal(2, cachedSender.SentMessages.Count);
        }

        private static Message CreateMessage(string topic) => new(
            new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_EVENT),
            new MessageBody("Message", new ContentType(MediaTypeNames.Application.Json)));
    }
}
