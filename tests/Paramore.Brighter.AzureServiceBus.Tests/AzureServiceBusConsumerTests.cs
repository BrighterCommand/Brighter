using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using FakeItEasy;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests
{
    public class AzureServiceBusConsumerTests
    {
        private readonly IAdministrationClientWrapper _nameSpaceManagerWrapper;
        private readonly AzureServiceBusConsumer _azureServiceBusConsumer;
        private readonly IServiceBusReceiverWrapper _messageReceiver;
        private readonly IAmAMessageProducerSync _mockMessageProducer;
        private readonly IServiceBusReceiverProvider _mockMessageReceiver;

        private readonly AzureServiceBusSubscriptionConfiguration _subConfig =
            new AzureServiceBusSubscriptionConfiguration();

        public AzureServiceBusConsumerTests()
        {
            _nameSpaceManagerWrapper = A.Fake<IAdministrationClientWrapper>();
            _mockMessageProducer = A.Fake<IAmAMessageProducerSync>();
            _mockMessageReceiver = A.Fake<IServiceBusReceiverProvider>();

            _messageReceiver = A.Fake<IServiceBusReceiverWrapper > ();

            A.CallTo(() =>
                    _mockMessageReceiver.Get("topic", "subscription", ServiceBusReceiveMode.ReceiveAndDelete, false))
                .Returns(_messageReceiver);

            var sub = new AzureServiceBusSubscription<ASBTestCommand>(routingKey: new RoutingKey("topic"), channelName: new ChannelName("subscription")
            ,makeChannels: OnMissingChannel.Create, bufferSize: 10, subscriptionConfiguration: _subConfig);
            
            _azureServiceBusConsumer = new AzureServiceBusTopicConsumer(sub, _mockMessageProducer,
                _nameSpaceManagerWrapper, _mockMessageReceiver);
        }

        [Fact]
        public void When_a_subscription_exists_and_messages_are_in_the_queue_the_messages_are_returned()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(true);

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();

            A.CallTo(() => message1.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "MT_EVENT" } });
            var message2 = A.Fake<IBrokeredMessageWrapper>();

            A.CallTo(() => message2.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody2"));
            A.CallTo(() => message2.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "MT_DOCUMENT" } });
            brokeredMessageList.Add(message1);
            brokeredMessageList.Add(message2);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

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
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(false);
            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();

            A.CallTo(() => message1.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "MT_EVENT" } });
            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            A.CallTo(() => _nameSpaceManagerWrapper.CreateSubscription("topic", "subscription", _subConfig)).MustHaveHappened();
            //A.CallTo(() => _nameSpaceManagerWrapper.f => f.CreateSubscription("topic", "subscription", _subConfig)).MustHaveHappened();
            Assert.Equal("somebody", result[0].Body.Value);
        }

        [Fact]
        public void When_a_message_is_a_command_type_then_the_message_type_is_set_correctly()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(true);

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();

            A.CallTo(() => message1.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "MT_COMMAND" } });
            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal("somebody", result[0].Body.Value);
            Assert.Equal("topic", result[0].Header.Topic);
            Assert.Equal(MessageType.MT_COMMAND, result[0].Header.MessageType);
        }

        [Fact]
        public void When_a_message_is_a_command_type_and_it_is_specified_in_funny_casing_then_the_message_type_is_set_correctly()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(true);

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();
            A.CallTo(() => message1.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "Mt_COmmAND" } });
            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal("somebody", result[0].Body.Value);
            Assert.Equal("topic", result[0].Header.Topic);
            Assert.Equal(MessageType.MT_COMMAND, result[0].Header.MessageType);
        }

        [Fact]
        public void When_the_specified_message_type_is_unknown_then_it_should_default_to_MT_EVENT()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(true);

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();

            A.CallTo(() => message1.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "wrong_message_type" } });
            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal(MessageType.MT_EVENT, result[0].Header.MessageType);
        }

        [Fact]
        public void When_the_message_type_is_not_specified_it_should_default_to_MT_EVENT()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(true);

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();
            A.CallTo(() => message1.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object>());
            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal("somebody", result[0].Body.Value);
            Assert.Equal("topic", result[0].Header.Topic);
            Assert.Equal(MessageType.MT_EVENT, result[0].Header.MessageType);
        }

        [Fact]
        public void When_the_user_properties_on_the_azure_sb_message_is_null_it_should_default_to_message_type_to_MT_EVENT()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(true);


            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();
            A.CallTo(() => message1.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object>());
            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal("somebody", result[0].Body.Value);
            Assert.Equal("topic", result[0].Header.Topic);
            Assert.Equal(MessageType.MT_EVENT, result[0].Header.MessageType);
        }

        [Fact]
        public void When_there_are_no_messages_then_it_returns_an_empty_array()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(true);
            var brokeredMessageList = new List<IBrokeredMessageWrapper>();

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);
            Assert.Empty(result);
        }

        [Fact]
        public void When_trying_to_create_a_subscription_which_was_already_created_by_another_thread_it_should_ignore_the_error()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(false);
            A.CallTo(() => _nameSpaceManagerWrapper.CreateSubscription("topic", "subscription", _subConfig))
                .Throws(new ServiceBusException("whatever", ServiceBusFailureReason.MessagingEntityAlreadyExists));

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();

            A.CallTo(() => message1.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "MT_EVENT" } });
            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            A.CallTo(() => _nameSpaceManagerWrapper.CreateSubscription("topic", "subscription", _subConfig)).MustHaveHappened();
            Assert.Equal("somebody", result[0].Body.Value);
        }

        [Fact]
        public void When_dispose_is_called_the_close_method_is_called()
        {
            _azureServiceBusConsumer.Dispose();

            A.CallTo(() => _messageReceiver.Close()).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public void When_requeue_is_called_and_the_delay_is_zero_the_send_method_is_called()
        {
            var messageLockTokenOne = Guid.NewGuid();
            var messageHeader = new MessageHeader(Guid.NewGuid().ToString(), "topic", MessageType.MT_EVENT);
            var message = new Message(messageHeader, new MessageBody("body"));
            message.Header.Bag.Add("LockToken", messageLockTokenOne);

            _azureServiceBusConsumer.Requeue(message, 0);

            A.CallTo(() => _mockMessageProducer.Send(message)).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public void When_requeue_is_called_and_the_delay_is_more_than_zero_the_sendWithDelay_method_is_called()
        {
            var messageLockTokenOne = Guid.NewGuid();
            var messageHeader = new MessageHeader(Guid.NewGuid().ToString(), "topic", MessageType.MT_EVENT);
            var message = new Message(messageHeader, new MessageBody("body"));
            message.Header.Bag.Add("LockToken", messageLockTokenOne);

            _azureServiceBusConsumer.Requeue(message, 100);

            A.CallTo(() => _mockMessageProducer.SendWithDelay(message, 100)).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public void
            When_there_is_an_error_talking_to_servicebus_when_checking_if_subscription_exist_then_a_ChannelFailureException_is_raised()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Throws(new Exception());

            Assert.Throws<ChannelFailureException>(() => _azureServiceBusConsumer.Receive(400));
        }

        [Fact]
        public void When_there_is_an_error_talking_to_servicebus_when_creating_the_subscription_then_a_ChannelFailureException_is_raised_and_ManagementClientWrapper_is_reinitilised()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(false);
            A.CallTo(() => _nameSpaceManagerWrapper.CreateSubscription("topic", "subscription", _subConfig)).Throws(new Exception());

            Assert.Throws<ChannelFailureException>(() => _azureServiceBusConsumer.Receive(400));
            A.CallTo(() => _nameSpaceManagerWrapper.Reset()).MustHaveHappenedOnceExactly();
        }

        /// <summary>
        /// TODO: review 
        /// </summary>
        [Fact]
        public void When_there_is_an_error_talking_to_servicebus_when_receiving_then_a_ChannelFailureException_is_raised_and_the_messageReceiver_is_recreated()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(true);

            A.CallTo(() => _messageReceiver.Receive(A<int>.Ignored, A<TimeSpan>.Ignored)).Throws<Exception>();

            Assert.Throws<ChannelFailureException>(() => _azureServiceBusConsumer.Receive(400));
            A.CallTo(() => _mockMessageReceiver.Get("topic", "subscription", ServiceBusReceiveMode.ReceiveAndDelete, false)).MustHaveHappened(2, Times.Exactly);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Once_the_subscription_is_created_or_exits_it_does_not_check_if_it_exists_every_time(bool subscriptionExists)
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(subscriptionExists);
            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();
            A.CallTo(() => message1.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "MT_EVENT" } });
            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            _azureServiceBusConsumer.Receive(400);
            _azureServiceBusConsumer.Receive(400);

            if (subscriptionExists == false)
            {
                A.CallTo(() => _nameSpaceManagerWrapper.CreateSubscription("topic", "subscription", _subConfig)).MustHaveHappened(1, Times.Exactly);
            }

            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public void When_MessagingEntityAlreadyExistsException_does_not_check_if_subscription_exists()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(false);
            A.CallTo(() => _nameSpaceManagerWrapper.CreateSubscription("topic", "subscription", new AzureServiceBusSubscriptionConfiguration()))
                .Throws(new ServiceBusException("whatever", ServiceBusFailureReason.MessagingEntityAlreadyExists));

            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();

            A.CallTo(() => message1.MessageBodyValue).Returns(Encoding.UTF8.GetBytes("somebody"));
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "MT_EVENT" } });
            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);
            _azureServiceBusConsumer.Receive(400);

            A.CallTo(() => _nameSpaceManagerWrapper.CreateSubscription("topic", "subscription", _subConfig)).MustHaveHappened();
            Assert.Equal("somebody", result[0].Body.Value);

            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).MustHaveHappened(1, Times.Exactly);
        }

        [Fact]
        public void When_a_message_contains_a_null_body_message_is_still_processed()
        {
            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();

            A.CallTo(() => message1.MessageBodyValue).Returns((byte[])null);
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "MT_EVENT" } });

            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal(string.Empty, result[0].Body.Value);
        }

        [Fact]
        public void When_receiving_messages_and_the_receiver_is_closing_a_MT_QUIT_message_is_sent()
        {
            A.CallTo(() => _messageReceiver.IsClosedOrClosing).Returns(true);
            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Throws(new Exception("Closing"));

            Message[] result = _azureServiceBusConsumer.Receive(400);

            Assert.Equal(MessageType.MT_QUIT, result[0].Header.MessageType);

        }

        [Fact]
        public void When_a_subscription_does_not_exist_and_Missing_is_set_to_Validate_a_Channel_Failure_is_Raised()
        {
            A.CallTo(() => _nameSpaceManagerWrapper.SubscriptionExists("topic", "subscription")).Returns(false);

            var sub = new AzureServiceBusSubscription<ASBTestCommand>(routingKey: new RoutingKey("topic"), channelName: new ChannelName("subscription")
                ,makeChannels: OnMissingChannel.Validate, subscriptionConfiguration: _subConfig);
            
            var azureServiceBusConsumerValidate = new AzureServiceBusTopicConsumer(sub, _mockMessageProducer,
                _nameSpaceManagerWrapper, _mockMessageReceiver);

            Assert.Throws<ChannelFailureException>(() => azureServiceBusConsumerValidate.Receive(400));
        }

        [Fact]
        public void When_ackOnRead_is_Set_and_ack_fails_then_exception_is_thrown()
        {
            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();
            var mockMessageReceiver = A.Fake<IServiceBusReceiverProvider>();

            A.CallTo(() => mockMessageReceiver.Get("topic", "subscription", ServiceBusReceiveMode.PeekLock, false)).Returns(_messageReceiver);

            var lockToken = Guid.NewGuid().ToString();

            A.CallTo(() => message1.MessageBodyValue).Returns((byte[])null);
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "MT_EVENT" } });
            A.CallTo(() => message1.LockToken).Returns(lockToken);

            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));
            A.CallTo(() => _messageReceiver.Complete(lockToken)).Throws(new Exception());

            var sub = new AzureServiceBusSubscription<ASBTestCommand>(routingKey: new RoutingKey("topic"), channelName: new ChannelName("subscription")
                ,makeChannels: OnMissingChannel.Create, bufferSize: 10, subscriptionConfiguration: _subConfig);
            
            var azureServiceBusConsumer = new AzureServiceBusTopicConsumer(sub, _mockMessageProducer,
                _nameSpaceManagerWrapper, mockMessageReceiver, receiveMode: ServiceBusReceiveMode.PeekLock);

            Message[] result = azureServiceBusConsumer.Receive(400);

            var msg = result.First();

            Assert.Throws<Exception>(() => azureServiceBusConsumer.Acknowledge(msg));
        }

        [Fact]
        public void When_ackOnRead_is_Set_and_DeadLetter_fails_then_exception_is_thrown()
        {
            var brokeredMessageList = new List<IBrokeredMessageWrapper>();
            var message1 = A.Fake<IBrokeredMessageWrapper>();
            var mockMessageReceiver = A.Fake<IServiceBusReceiverProvider>();

            A.CallTo(() => mockMessageReceiver.Get("topic", "subscription", ServiceBusReceiveMode.PeekLock, false)).Returns(_messageReceiver);

            var lockToken = Guid.NewGuid().ToString();

            A.CallTo(() => message1.MessageBodyValue).Returns((byte[])null);
            A.CallTo(() => message1.ApplicationProperties).Returns(new Dictionary<string, object> { { "MessageType", "MT_EVENT" } });
            A.CallTo(() => message1.LockToken).Returns(lockToken);

            brokeredMessageList.Add(message1);

            A.CallTo(() => _messageReceiver.Receive(10, TimeSpan.FromMilliseconds(400))).Returns(Task.FromResult<IEnumerable<IBrokeredMessageWrapper>>(brokeredMessageList));
            A.CallTo(() => _messageReceiver.DeadLetter(lockToken)).Throws(new Exception());

            var sub = new AzureServiceBusSubscription<ASBTestCommand>(routingKey: new RoutingKey("topic"), channelName: new ChannelName("subscription")
                ,makeChannels: OnMissingChannel.Create, bufferSize: 10, subscriptionConfiguration: _subConfig);
            
            var azureServiceBusConsumer = new AzureServiceBusTopicConsumer(sub, _mockMessageProducer,
                _nameSpaceManagerWrapper, mockMessageReceiver, receiveMode: ServiceBusReceiveMode.PeekLock);

            Message[] result = azureServiceBusConsumer.Receive(400);

            var msg = result.First();

            Assert.Throws<Exception>(() => azureServiceBusConsumer.Reject(msg));
        }
    }
}
