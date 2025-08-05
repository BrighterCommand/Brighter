using System;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway.Reactor
{
    public class AzureServiceBusMessageProducerTests
    {
        private readonly FakeAdministrationClient _nameSpaceManagerWrapper;
        private readonly FakeServiceBusSenderProvider _topicClientProvider;
        private readonly FakeServiceBusSenderWrapper _topicClient;
        private readonly AzureServiceBusMessageProducer _producer;
        private readonly AzureServiceBusMessageProducer _queueProducer;

        public AzureServiceBusMessageProducerTests()
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
        public void When_the_topic_exists_and_sending_a_message_with_no_delay_it_should_send_the_message_to_the_correct_topicclient()
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            
            _producer.Send(new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_EVENT), 
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json)))
            );
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_EVENT", sentMessage.ApplicationProperties["MessageType"]);
            Assert.Equal(1, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void When_sending_a_command_message_type_message_with_no_delay_it_should_set_the_correct_messagetype_property(bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            _nameSpaceManagerWrapper.Queues.Add("topic");

            var producer = useQueues ? _queueProducer : _producer;
            
            producer.Send(new Message(
                new MessageHeader(Id.Random(),  new RoutingKey("topic"), MessageType.MT_COMMAND), 
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json)))
            );
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_COMMAND", sentMessage.ApplicationProperties["MessageType"]);
            Assert.Equal(1, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void When_the_topic_does_not_exist_it_should_be_created_and_the_message_is_sent_to_the_correct_topicclient(bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();

            var producer = useQueues ? _queueProducer : _producer;
            
            producer.Send(new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE), 
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))));
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(1, _nameSpaceManagerWrapper.CreateCount);
            Assert.Equal(messageBody, sentMessage.Body.ToArray());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void When_a_message_is_send_and_an_exception_occurs_close_is_still_called(bool useQueues)
        {
            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            _nameSpaceManagerWrapper.Queues.Add("topic");

            _topicClient.SendException = new Exception("Failed");

            try
            {
                var producer = useQueues ? _queueProducer : _producer;
                
                producer.Send(new Message(
                    new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE), 
                    new MessageBody("Message", new ContentType(MediaTypeNames.Application.Json))));
            }
            catch (Exception)
            {
                // ignored
            }

            Assert.Equal(1, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void
            When_the_topic_exists_and_sending_a_message_with_a_delay_it_should_send_the_message_to_the_correct_topicclient(bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            _nameSpaceManagerWrapper.Queues.Add("topic");

            var producer = useQueues ? _queueProducer : _producer;
            
            producer.SendWithDelay(
                new Message(
                    new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_EVENT),
                    new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))), TimeSpan.FromSeconds(1));
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_EVENT", sentMessage.ApplicationProperties["MessageType"]);
            Assert.Equal(1, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void
            When_sending_a_command_message_type_message_with_delay_it_should_set_the_correct_messagetype_property(
                bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();
            _nameSpaceManagerWrapper.Topics.Add("topic", []);
            _nameSpaceManagerWrapper.Queues.Add("topic");

            var producer = useQueues ? _queueProducer : _producer;

            producer.SendWithDelay(new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_COMMAND),
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))), TimeSpan.FromSeconds(1));
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_COMMAND", sentMessage.ApplicationProperties["MessageType"]);
            Assert.Equal(1, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void
            When_the_topic_does_not_exist_and_sending_a_message_with_a_delay_it_should_send_the_message_to_the_correct_topicclient(
                bool useQueues)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.ResetState();

            var producer = useQueues ? _queueProducer : _producer;

            producer.SendWithDelay(new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE),
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))), TimeSpan.FromSeconds(1));
            
            ServiceBusMessage sentMessage = _topicClient.SentMessages.First();

            Assert.Equal(1, _nameSpaceManagerWrapper.CreateCount);
            
            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal(1, _topicClient.ClosedCount);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void Once_the_topic_is_created_it_then_does_not_check_if_it_exists_every_time(bool topicExists, bool useQueues)
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
            
            producer.SendWithDelay(new Message(
                new MessageHeader(Id.Random(), routingKey, MessageType.MT_NONE), 
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))), TimeSpan.FromSeconds(1));
            producer.SendWithDelay(new Message(
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
    }
}
