using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Moq;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests
{
    public class AzureServiceBusConsumerTests
    {
        private readonly Mock<IManagementClientWrapper> _nameSpaceManagerWrapper;
        private readonly AzureServiceBusConsumer _azureServiceBusConsumer;
        private readonly Mock<IMessageReceiverWrapper> _messageReceiver;
        private readonly Mock<IAmAMessageProducer> _mockMessageProducer;
        private readonly Mock<IMessageReceiverProvider> _mockMessageReceiver;

        public AzureServiceBusConsumerTests()
        {
            _nameSpaceManagerWrapper = new Mock<IManagementClientWrapper>();
            _mockMessageProducer = new Mock<IAmAMessageProducer>();
            _mockMessageReceiver = new Mock<IMessageReceiverProvider>();

            _messageReceiver = new Mock<IMessageReceiverWrapper>();

            _mockMessageReceiver.Setup(x => x.Get("topic", "subscription", ReceiveMode.ReceiveAndDelete)).Returns(_messageReceiver.Object);

            _azureServiceBusConsumer = new AzureServiceBusConsumer("topic", "subscription", _mockMessageProducer.Object,
                _nameSpaceManagerWrapper.Object, _mockMessageReceiver.Object, makeChannels: OnMissingChannel.Create);
        }

        [Fact]
        public void When_a_subscription_exists_and_messages_are_in_the_queue_the_messages_are_returned()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(true);

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();

            message1.Setup(m => m.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            message1.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>() { { "MessageType", "MT_EVENT" } });
            var message2 = new Mock<IBrokeredMessageWrapper>();

            message2.Setup(m => m.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody2"));
            message2.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>() { { "MessageType", "MT_DOCUMENT" } });
            brokeredMessageList.Add(message1.Object);
            brokeredMessageList.Add(message2.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal("somebody", result[0].Body.Value);
            Assert.Equal("topic", result[0].Header.Topic);
            Assert.Equal(MessageType.MT_EVENT, result[0].Header.MessageType);

            Assert.Equal("somebody2", result[1].Body.Value);
            Assert.Equal("topic", result[1].Header.Topic);
            Assert.Equal(MessageType.MT_DOCUMENT, result[1].Header.MessageType);
        }

        [Fact]
        public void When_a_subscription_does_not_exist_and_messages_are_in_the_queue_then_the_subscription_is_created_and_messages_are_returned()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(false);
            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();

            message1.Setup(m => m.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            message1.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>() { { "MessageType", "MT_EVENT" } });
            brokeredMessageList.Add(message1.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            _nameSpaceManagerWrapper.Verify(f => f.CreateSubscription("topic", "subscription", 2000));
            Assert.Equal("somebody", result[0].Body.Value);
        }

        [Fact]
        public void When_a_message_is_a_command_type_then_the_message_type_is_set_correctly()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(true);

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();

            message1.Setup(m => m.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            message1.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>() { { "MessageType", "MT_COMMAND" } });
            brokeredMessageList.Add(message1.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal("somebody", result[0].Body.Value);
            Assert.Equal("topic", result[0].Header.Topic);
            Assert.Equal(MessageType.MT_COMMAND, result[0].Header.MessageType);
        }

        [Fact]
        public void When_a_message_is_a_command_type_and_it_is_specified_in_funny_casing_then_the_message_type_is_set_correctly()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(true);

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();
            message1.Setup(m => m.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            message1.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>() { { "MessageType", "Mt_COmmAND" } });
            brokeredMessageList.Add(message1.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal("somebody", result[0].Body.Value);
            Assert.Equal("topic", result[0].Header.Topic);
            Assert.Equal(MessageType.MT_COMMAND, result[0].Header.MessageType);
        }

        [Fact]
        public void When_the_specified_message_type_is_unknown_then_it_should_default_to_MT_EVENT()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(true);

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();

            message1.Setup(m => m.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            message1.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>() { { "MessageType", "wrong_message_type" } });
            brokeredMessageList.Add(message1.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal(MessageType.MT_EVENT, result[0].Header.MessageType);
        }

        [Fact]
        public void When_the_message_type_is_not_specified_it_should_default_to_MT_EVENT()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(true);

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();
            message1.Setup(m => m.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            message1.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>());
            brokeredMessageList.Add(message1.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal("somebody", result[0].Body.Value);
            Assert.Equal("topic", result[0].Header.Topic);
            Assert.Equal(MessageType.MT_EVENT, result[0].Header.MessageType);
        }

        [Fact]
        public void When_the_user_properties_on_the_azure_sb_message_is_null_it_should_default_to_message_type_to_MT_EVENT()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(true);


            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();
            message1.Setup(m => m.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            message1.Setup(m => m.UserProperties).Returns(null as IDictionary<string, object>);
            brokeredMessageList.Add(message1.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal("somebody", result[0].Body.Value);
            Assert.Equal("topic", result[0].Header.Topic);
            Assert.Equal(MessageType.MT_EVENT, result[0].Header.MessageType);
        }

        [Fact]
        public void When_there_are_no_messages_then_it_returns_an_empty_array()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(true);
            var brokeredMessageList = new List<IBrokeredMessageWrapper>();

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);
            Assert.Empty(result);
        }

        [Fact]
        public void When_trying_to_create_a_subscription_which_was_already_created_by_another_thread_it_should_ignore_the_error()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(false);
            _nameSpaceManagerWrapper.Setup(f => f.CreateSubscription("topic", "subscription", 2000)).Throws(new MessagingEntityAlreadyExistsException("whatever"));

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();

            message1.Setup(m => m.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            message1.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>() { { "MessageType", "MT_EVENT" } });
            brokeredMessageList.Add(message1.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            _nameSpaceManagerWrapper.Verify(f => f.CreateSubscription("topic", "subscription", 2000));
            Assert.Equal("somebody", result[0].Body.Value);
        }

        [Fact]
        public void When_reject_is_called_with_requeue_the_message_requeued()
        {
            var messageHeader = new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_EVENT);

            var message = new Message(messageHeader, new MessageBody("body"));

            _azureServiceBusConsumer.Reject(message, true);

            _mockMessageProducer.Verify(x => x.Send(message), Times.Once);
        }

        [Fact]
        public void When_dispose_is_called_the_close_method_is_called()
        {
            _azureServiceBusConsumer.Dispose();

            _messageReceiver.Verify(x => x.Close(), Times.Once);
        }

        [Fact]
        public void When_requeue_is_called_and_the_delay_is_zero_the_send_method_is_called()
        {
            var messageLockTokenOne = Guid.NewGuid();
            var messageHeader = new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_EVENT);
            var message = new Message(messageHeader, new MessageBody("body"));
            message.Header.Bag.Add("LockToken", messageLockTokenOne);

            _azureServiceBusConsumer.Requeue(message, 0);

            _mockMessageProducer.Verify(x => x.Send(message), Times.Once);
        }

        [Fact]
        public void When_requeue_is_called_and_the_delay_is_more_than_zero_the_sendWithDelay_method_is_called()
        {
            var messageLockTokenOne = Guid.NewGuid();
            var messageHeader = new MessageHeader(Guid.NewGuid(), "topic", MessageType.MT_EVENT);
            var message = new Message(messageHeader, new MessageBody("body"));
            message.Header.Bag.Add("LockToken", messageLockTokenOne);

            _azureServiceBusConsumer.Requeue(message, 100);

            _mockMessageProducer.Verify(x => x.SendWithDelay(message, 100), Times.Once);
        }

        [Fact]
        public void
            When_there_is_an_error_talking_to_servicebus_when_checking_if_subscription_exist_then_a_ChannelFailureException_is_raised()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Throws(new Exception());

            Assert.Throws<ChannelFailureException>(() => _azureServiceBusConsumer.Receive(400));
        }

        [Fact]
        public void When_there_is_an_error_talking_to_servicebus_when_creating_the_subscription_then_a_ChannelFailureException_is_raised_and_ManagementClientWrapper_is_reinitilised()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(false);
            _nameSpaceManagerWrapper.Setup(f => f.CreateSubscription("topic", "subscription", 2000)).Throws(new Exception());

            Assert.Throws<ChannelFailureException>(() => _azureServiceBusConsumer.Receive(400));
            _nameSpaceManagerWrapper.Verify(managementClientWrapper => managementClientWrapper.Reset(), Times.Once);
        }

        [Fact]
        public void When_there_is_an_error_talking_to_servicebus_when_receiving_then_a_ChannelFailureException_is_raised_and_the_messageReceiver_is_recreated()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(true);

            _messageReceiver.Setup(f => f.Receive(It.IsAny<int>(), It.IsAny<TimeSpan>())).Throws(new Exception());

            Assert.Throws<ChannelFailureException>(() => _azureServiceBusConsumer.Receive(400));
            _mockMessageReceiver.Verify(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReceiveMode>()), Times.Exactly(2));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Once_the_subscription_is_created_or_exits_it_does_not_check_if_it_exists_every_time(bool subscriptionExists)
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(subscriptionExists);
            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();
            message1.Setup(m => m.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            message1.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>() { { "MessageType", "MT_EVENT" } });
            brokeredMessageList.Add(message1.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            _azureServiceBusConsumer.Receive(400);
            _azureServiceBusConsumer.Receive(400);

            if (subscriptionExists == false)
            {
                _nameSpaceManagerWrapper.Verify(f => f.CreateSubscription("topic", "subscription", 2000), Times.Once);
            }

            _nameSpaceManagerWrapper.Verify(f => f.SubscriptionExists("topic", "subscription"), Times.Once);
        }

        [Fact]
        public void When_MessagingEntityAlreadyExistsException_does_not_check_if_subscription_exists()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(false);
            _nameSpaceManagerWrapper.Setup(f => f.CreateSubscription("topic", "subscription", 2000)).Throws(new MessagingEntityAlreadyExistsException("whatever"));

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();

            message1.Setup(m => m.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            message1.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>() { { "MessageType", "MT_EVENT" } });
            brokeredMessageList.Add(message1.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);
            _azureServiceBusConsumer.Receive(400);

            _nameSpaceManagerWrapper.Verify(f => f.CreateSubscription("topic", "subscription", 2000));
            Assert.Equal("somebody", result[0].Body.Value);

            _nameSpaceManagerWrapper.Verify(f => f.SubscriptionExists("topic", "subscription"), Times.Once);
        }

        [Fact]
        public void When_a_message_contains_a_null_body_message_is_still_processed()
        {
            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();

            message1.Setup(x => x.MessageBodyValue).Returns((byte[])null);
            message1.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>() { { "MessageType", "MT_EVENT" } });

            brokeredMessageList.Add(message1.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal(string.Empty, result[0].Body.Value);
        }

        [Fact]
        public void When_receiving_messages_and_the_receiver_is_closing_a_MT_QUIT_message_is_sent()
        {
            _messageReceiver.Setup(x => x.IsClosedOrClosing).Returns(true);
            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Throws(new Exception("Closing"));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal(MessageType.MT_QUIT, result[0].Header.MessageType);

        }

        [Fact]
        public void When_a_subscription_does_not_exist_and_Missing_is_set_to_Validate_a_Channel_Failure_is_Raised()
        {
            _nameSpaceManagerWrapper.Setup(f => f.SubscriptionExists("topic", "subscription")).Returns(false);

            var azureServiceBusConsumerValidate = new AzureServiceBusConsumer("topic", "subscription", _mockMessageProducer.Object,
                _nameSpaceManagerWrapper.Object, _mockMessageReceiver.Object, makeChannels: OnMissingChannel.Validate);

            Assert.Throws<ChannelFailureException>(() => azureServiceBusConsumerValidate.Receive(400));
        }

        [Fact]
        public void When_ackOnRead_is_Set_and_ack_fails_then_exception_is_thrown()
        {
            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = new Mock<IBrokeredMessageWrapper>();
            var mockMessageReceiver = new Mock<IMessageReceiverProvider>();

            mockMessageReceiver.Setup(x => x.Get("topic", "subscription", ReceiveMode.PeekLock)).Returns(_messageReceiver.Object);

            var lockToken = Guid.NewGuid().ToString();

            message1.Setup(x => x.MessageBodyValue).Returns((byte[])null);
            message1.Setup(m => m.UserProperties).Returns(new Dictionary<string, object>() { { "MessageType", "MT_EVENT" } });
            message1.Setup(m => m.LockToken).Returns(lockToken);

            brokeredMessageList.Add(message1.Object);

            _messageReceiver.Setup(x => x.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));
            _messageReceiver.Setup(x => x.Complete(lockToken)).Throws(new Exception());

            var azureServiceBusConsumer = new AzureServiceBusConsumer("topic", "subscription", _mockMessageProducer.Object,
                _nameSpaceManagerWrapper.Object, mockMessageReceiver.Object, makeChannels: OnMissingChannel.Create, receiveMode: ReceiveMode.PeekLock);

            Message[] result = azureServiceBusConsumer.Receive(400);

            var msg = result.First();

            Assert.Throws<Exception>(() => azureServiceBusConsumer.Acknowledge(msg));
        }
    }
}
