using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using FakeItEasy;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests
{
    public class AzureServiceBusMessageProducerTests
    {
        private readonly IAdministrationClientWrapper _nameSpaceManagerWrapper;
        private readonly IServiceBusSenderProvider _topicClientProvider;
        private readonly IServiceBusSenderWrapper _topicClient;
        private readonly AzureServiceBusMessageProducer _producer;

        public AzureServiceBusMessageProducerTests()
        {
            _nameSpaceManagerWrapper = A.Fake<IAdministrationClientWrapper>();
            _topicClientProvider = A.Fake<IServiceBusSenderProvider>();
            _topicClient = A.Fake<IServiceBusSenderWrapper>();

            _producer = new AzureServiceBusMessageProducer(_nameSpaceManagerWrapper, _topicClientProvider, OnMissingChannel.Create);
        }

        [Fact]
        public void When_the_topic_exists_and_sending_a_message_with_no_delay_it_should_send_the_message_to_the_correct_topicclient()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            A.CallTo(() => _nameSpaceManagerWrapper.TopicExists("topic")).Returns(true);
            A.CallTo(() => _topicClientProvider.Get("topic")).Returns(_topicClient);
            //A.CallTo(() => _topicClient.SendAsync(A<ServiceBusMessage>.Ignored, CancellationToken.None)).ReturnsLazily(() => (ServiceBusMessage g, CancellationToken ct) => sentMessage = g);
            A.CallTo(() => _topicClient.SendAsync(A<ServiceBusMessage>.Ignored, CancellationToken.None)).ReturnsLazily((ServiceBusMessage g, CancellationToken ct) => Task.FromResult(sentMessage = g));

            _producer.Send(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_EVENT), new MessageBody(messageBody, "JSON")));

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_EVENT", sentMessage.ApplicationProperties["MessageType"]);
            A.CallTo(() => _topicClient.CloseAsync()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void When_sending_a_command_message_type_message_with_no_delay_it_should_set_the_correct_messagetype_property()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            A.CallTo(() => _nameSpaceManagerWrapper.TopicExists("topic")).Returns(true);
            A.CallTo(() => _topicClientProvider.Get("topic")).Returns(_topicClient);
            //A.CallTo(() => _topicClient.SendAsync(A<ServiceBusMessage>.Ignored, CancellationToken.None)).Callback((ServiceBusMessage g, CancellationToken ct) => sentMessage = g);
            A.CallTo(() => _topicClient.SendAsync(A<ServiceBusMessage>.Ignored, CancellationToken.None)).ReturnsLazily((ServiceBusMessage g, CancellationToken ct) => Task.FromResult(sentMessage = g));

            _producer.Send(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_COMMAND), new MessageBody(messageBody, "JSON")));

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_COMMAND", sentMessage.ApplicationProperties["MessageType"]);
            A.CallTo(() => _topicClient.CloseAsync()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void When_the_topic_does_not_exist_it_should_be_created_and_the_message_is_sent_to_the_correct_topicclient()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            A.CallTo(() => _nameSpaceManagerWrapper.TopicExists("topic")).Returns(false);
            A.CallTo(() => _topicClientProvider.Get("topic")).Returns(_topicClient);
            //A.CallTo(() => _topicClient.SendAsync(A<ServiceBusMessage>.Ignored, CancellationToken.None)).Callback((ServiceBusMessage g, CancellationToken ct) => sentMessage = g);
            A.CallTo(() => _topicClient.SendAsync(A<ServiceBusMessage>.Ignored, CancellationToken.None)).ReturnsLazily((ServiceBusMessage g, CancellationToken ct) => Task.FromResult(sentMessage = g));

            _producer.Send(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")));

            A.CallTo(() => _nameSpaceManagerWrapper.CreateTopic("topic", null)).MustHaveHappenedOnceExactly();
            Assert.Equal(messageBody, sentMessage.Body.ToArray());
        }

        [Fact]
        public void When_a_message_is_send_and_an_exception_occurs_close_is_still_called()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.TopicExists("topic")).Returns(true);
            A.CallTo(() => _topicClientProvider.Get("topic")).Returns(_topicClient);

            A.CallTo(() => _topicClient.SendAsync(A<ServiceBusMessage>.Ignored, CancellationToken.None)).Throws(new Exception("Failed"));

            try
            {
                _producer.Send(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody("Message", "JSON")));
            }
            catch (Exception)
            {
                // ignored
            }

            A.CallTo(() => _topicClient.CloseAsync()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void
            When_the_topic_exists_and_sending_a_message_with_a_delay_it_should_send_the_message_to_the_correct_topicclient()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            A.CallTo(() => _nameSpaceManagerWrapper.TopicExists("topic")).Returns(true);
            A.CallTo(() => _topicClientProvider.Get("topic")).Returns(_topicClient);

            A.CallTo(() => _topicClient.ScheduleMessageAsync(A<ServiceBusMessage>.Ignored, A<DateTimeOffset>.Ignored,
                CancellationToken.None)).ReturnsLazily((ServiceBusMessage g, DateTimeOffset t, CancellationToken ct) =>
                Task.FromResult(sentMessage = g));

            _producer.SendWithDelay(
                new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_EVENT),
                    new MessageBody(messageBody, "JSON")), 1);

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_EVENT", sentMessage.ApplicationProperties["MessageType"]);
            A.CallTo(() => _topicClient.CloseAsync()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void When_sending_a_command_message_type_message_with_delay_it_should_set_the_correct_messagetype_property()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            A.CallTo(() => _nameSpaceManagerWrapper.TopicExists("topic")).Returns(true);
            A.CallTo(() => _topicClientProvider.Get("topic")).Returns(_topicClient);
            
                A.CallTo(() => _topicClient.ScheduleMessageAsync(A<ServiceBusMessage>.Ignored, A<DateTimeOffset>.Ignored,
                    CancellationToken.None)).ReturnsLazily((ServiceBusMessage g, DateTimeOffset t, CancellationToken ct) => Task.FromResult(sentMessage = g));

            _producer.SendWithDelay(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_COMMAND), new MessageBody(messageBody, "JSON")), 1);

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_COMMAND", sentMessage.ApplicationProperties["MessageType"]);
            A.CallTo(() => _topicClient.CloseAsync()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void When_the_topic_does_not_exist_and_sending_a_message_with_a_delay_it_should_send_the_message_to_the_correct_topicclient()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            A.CallTo(() => _nameSpaceManagerWrapper.TopicExists("topic")).Returns(false);
            A.CallTo(() => _topicClientProvider.Get("topic")).Returns(_topicClient);
            
                A.CallTo(() => _topicClient.ScheduleMessageAsync(A<ServiceBusMessage>.Ignored, A<DateTimeOffset>.Ignored,
                    CancellationToken.None)).ReturnsLazily((ServiceBusMessage g, DateTimeOffset t , CancellationToken ct) => Task.FromResult(sentMessage = g));

            _producer.SendWithDelay(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")), 1);

            A.CallTo(() => _nameSpaceManagerWrapper.CreateTopic("topic", null)).MustHaveHappenedOnceExactly();
            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            A.CallTo(() => _topicClient.CloseAsync()).MustHaveHappenedOnceExactly();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Once_the_topic_is_created_it_then_does_not_check_if_it_exists_every_time(bool topicExists)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            A.CallTo(() => _nameSpaceManagerWrapper.TopicExists("topic")).Returns(topicExists);
            A.CallTo(() => _topicClientProvider.Get("topic")).Returns(_topicClient);
            //A.CallTo(() => _topicClient.ScheduleMessage(A<ServiceBusMessage>.Ignored, A<DateTimeOffset>.Ignored));

            _producer.SendWithDelay(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")), 1);
            _producer.SendWithDelay(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")), 1);

            if (topicExists == false)
            {
                A.CallTo(() => _nameSpaceManagerWrapper.CreateTopic("topic", null)).MustHaveHappenedOnceExactly();
            }

            A.CallTo(() => _nameSpaceManagerWrapper.TopicExists("topic")).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void When_there_is_an_error_talking_to_servicebus_when_creating_the_topic_the_ManagementClientWrapper_is_reinitilised()
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            A.CallTo(() => _nameSpaceManagerWrapper.TopicExists("topic")).Throws(new Exception());

            Assert.ThrowsAsync<Exception>(() => _producer.SendWithDelayAsync(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")), 1));
            A.CallTo(() => _nameSpaceManagerWrapper.Reset()).MustHaveHappenedOnceExactly();
        }


        [Fact]
        public void When_there_is_an_error_getting_a_topic_client_the_connection_for_topic_client_is_retried()
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            A.CallTo(() => _nameSpaceManagerWrapper.TopicExists("topic")).Returns(true);

            // _topicClientProvider.SetupSequence(f => f.Get("topic"))
            //     .Throws(new Exception())
            //     .Returns(_topicClient);


            A.CallTo(() => _topicClientProvider.Get("topic")).Throws(new Exception()).Once().Then.Returns(_topicClient);

           _producer.SendWithDelay(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")));

            A.CallTo(() => _topicClient.SendAsync(A<ServiceBusMessage>.Ignored, CancellationToken.None)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void When_the_topic_does_not_exist_and_Missing_is_set_to_Validate_an_exception_is_raised()
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            var producerValidate = new AzureServiceBusMessageProducer(_nameSpaceManagerWrapper, _topicClientProvider, OnMissingChannel.Validate);

            Assert.ThrowsAsync<ChannelFailureException>(() => producerValidate.SendAsync(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON"))));
        }
    }
}
