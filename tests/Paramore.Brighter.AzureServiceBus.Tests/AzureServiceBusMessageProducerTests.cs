using System;
using System.Text;
using System.Threading;
using Azure.Messaging.ServiceBus;
using Moq;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests
{
    public class AzureServiceBusMessageProducerTests
    {
        private readonly Mock<IAdministrationClientWrapper> _nameSpaceManagerWrapper;
        private readonly Mock<IServiceBusSenderProvider> _topicClientProvider;
        private readonly Mock<IServiceBusSenderWrapper> _topicClient;
        private readonly AzureServiceBusMessageProducer _producer;

        public AzureServiceBusMessageProducerTests()
        {
            _nameSpaceManagerWrapper = new Mock<IAdministrationClientWrapper>();
            _topicClientProvider = new Mock<IServiceBusSenderProvider>();
            _topicClient = new Mock<IServiceBusSenderWrapper>();

            _producer = new AzureServiceBusMessageProducer(_nameSpaceManagerWrapper.Object, _topicClientProvider.Object, OnMissingChannel.Create);
        }

        [Fact]
        public void When_the_topic_exists_and_sending_a_message_with_no_delay_it_should_send_the_message_to_the_correct_topicclient()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.Setup(t => t.TopicExists("topic")).Returns(true);
            _topicClientProvider.Setup(f => f.Get("topic")).Returns(_topicClient.Object);
            _topicClient.Setup(f => f.SendAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None))
                .Callback((ServiceBusMessage g, CancellationToken ct) => sentMessage = g);

            _producer.Send(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_EVENT), new MessageBody(messageBody, "JSON")));

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_EVENT", sentMessage.ApplicationProperties["MessageType"]);
            _topicClient.Verify(x => x.CloseAsync(), Times.Once);
        }

        [Fact]
        public void When_sending_a_command_message_type_message_with_no_delay_it_should_set_the_correct_messagetype_property()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.Setup(t => t.TopicExists("topic")).Returns(true);
            _topicClientProvider.Setup(f => f.Get("topic")).Returns(_topicClient.Object);
            _topicClient.Setup(f => f.SendAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None))
                .Callback((ServiceBusMessage g, CancellationToken ct) => sentMessage = g);

            _producer.Send(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_COMMAND), new MessageBody(messageBody, "JSON")));

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_COMMAND", sentMessage.ApplicationProperties["MessageType"]);
            _topicClient.Verify(x => x.CloseAsync(), Times.Once);
        }

        [Fact]
        public void When_the_topic_does_not_exist_it_should_be_created_and_the_message_is_sent_to_the_correct_topicclient()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.Setup(t => t.TopicExists("topic")).Returns(false);
            _topicClientProvider.Setup(f => f.Get("topic")).Returns(_topicClient.Object);
            _topicClient.Setup(f => f.SendAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None)).Callback((ServiceBusMessage g, CancellationToken ct) => sentMessage = g);

            _producer.Send(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")));

            _nameSpaceManagerWrapper.Verify(x => x.CreateTopic("topic"), Times.Once);
            Assert.Equal(messageBody, sentMessage.Body.ToArray());
        }

        [Fact]
        public void When_a_message_is_send_and_an_exception_occurs_close_is_still_called()
        {
            _nameSpaceManagerWrapper.Setup(t => t.TopicExists("topic")).Returns(true);
            _topicClientProvider.Setup(f => f.Get("topic")).Returns(_topicClient.Object);

            _topicClient.Setup(x => x.SendAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None)).Throws(new Exception("Failed"));

            try
            {
                _producer.Send(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody("Message", "JSON")));
            }
            catch (Exception)
            {
                // ignored
            }

            _topicClient.Verify(x => x.CloseAsync(), Times.Once);
        }

        [Fact]
        public void When_the_topic_exists_and_sending_a_message_with_a_delay_it_should_send_the_message_to_the_correct_topicclient()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.Setup(t => t.TopicExists("topic")).Returns(true);
            _topicClientProvider.Setup(f => f.Get("topic")).Returns(_topicClient.Object);
            _topicClient
                .Setup(f => f.ScheduleMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<DateTimeOffset>(),
                    CancellationToken.None)).Callback((ServiceBusMessage g, DateTimeOffset d, CancellationToken ct) =>
                    sentMessage = g);

            _producer.SendWithDelay(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_EVENT), new MessageBody(messageBody, "JSON")), 1);

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_EVENT", sentMessage.ApplicationProperties["MessageType"]);
            _topicClient.Verify(x => x.CloseAsync(), Times.Once);
        }

        [Fact]
        public void When_sending_a_command_message_type_message_with_delay_it_should_set_the_correct_messagetype_property()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.Setup(t => t.TopicExists("topic")).Returns(true);
            _topicClientProvider.Setup(f => f.Get("topic")).Returns(_topicClient.Object);
            _topicClient
                .Setup(f => f.ScheduleMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<DateTimeOffset>(),
                    CancellationToken.None)).Callback((ServiceBusMessage g, DateTimeOffset d, CancellationToken ct) =>
                    sentMessage = g);

            _producer.SendWithDelay(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_COMMAND), new MessageBody(messageBody, "JSON")), 1);

            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            Assert.Equal("MT_COMMAND", sentMessage.ApplicationProperties["MessageType"]);
            _topicClient.Verify(x => x.CloseAsync(), Times.Once);
        }

        [Fact]
        public void When_the_topic_does_not_exist_and_sending_a_message_with_a_delay_it_should_send_the_message_to_the_correct_topicclient()
        {
            ServiceBusMessage sentMessage = null;
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.Setup(t => t.TopicExists("topic")).Returns(false);
            _topicClientProvider.Setup(f => f.Get("topic")).Returns(_topicClient.Object);
            _topicClient
                .Setup(f => f.ScheduleMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<DateTimeOffset>(),
                    CancellationToken.None)).Callback((ServiceBusMessage g, DateTimeOffset d, CancellationToken ct) =>
                    sentMessage = g);

            _producer.SendWithDelay(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")), 1);

            _nameSpaceManagerWrapper.Verify(x => x.CreateTopic("topic"), Times.Once);
            Assert.Equal(messageBody, sentMessage.Body.ToArray());
            _topicClient.Verify(x => x.CloseAsync(), Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Once_the_topic_is_created_it_then_does_not_check_if_it_exists_every_time(bool topicExists)
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.Setup(t => t.TopicExists("topic")).Returns(topicExists);
            _topicClientProvider.Setup(f => f.Get("topic")).Returns(_topicClient.Object);
            _topicClient.Setup(f => f.ScheduleMessage(It.IsAny<ServiceBusMessage>(), It.IsAny<DateTimeOffset>())).Callback((ServiceBusMessage g, DateTimeOffset d) => { });

            _producer.SendWithDelay(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")), 1);
            _producer.SendWithDelay(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")), 1);

            if (topicExists == false)
            {
                _nameSpaceManagerWrapper.Verify(x => x.CreateTopic("topic"), Times.Once);
            }

            _nameSpaceManagerWrapper.Verify(x => x.TopicExists("topic"), Times.Once);
        }

        [Fact]
        public void When_there_is_an_error_talking_to_servicebus_when_creating_the_topic_the_ManagementClientWrapper_is_reinitilised()
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.Setup(t => t.TopicExists("topic")).Throws(new Exception());

            Assert.ThrowsAsync<Exception>(() => _producer.SendWithDelayAsync(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")), 1));
            _nameSpaceManagerWrapper.Verify(managementClientWrapper => managementClientWrapper.Reset(), Times.Once);
        }


        [Fact]
        public void When_there_is_an_error_getting_a_topic_client_the_connection_for_topic_client_is_retried()
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            _nameSpaceManagerWrapper.Setup(t => t.TopicExists("topic")).Returns(true);

            _topicClientProvider.SetupSequence(f => f.Get("topic"))
                .Throws(new Exception())
                .Returns(_topicClient.Object);

            _producer.SendWithDelay(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON")));

            _topicClient.Verify(topicClient => topicClient.SendAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None), Times.Once);
        }

        [Fact]
        public void When_the_topic_does_not_exist_and_Missing_is_set_to_Validate_an_exception_is_raised()
        {
            var messageBody = Encoding.UTF8.GetBytes("A message body");

            var producerValidate = new AzureServiceBusMessageProducer(_nameSpaceManagerWrapper.Object, _topicClientProvider.Object, OnMissingChannel.Validate);

            Assert.ThrowsAsync<ChannelFailureException>(() => producerValidate.SendAsync(new Message(new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_NONE), new MessageBody(messageBody, "JSON"))));
        }
    }
}
