using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway.Proactor;

public class AzureServiceBusConsumerTestsAsync
{
    private readonly FakeAdministrationClient _nameSpaceManagerWrapper;
    private readonly AzureServiceBusConsumer _azureServiceBusConsumer;
    private readonly FakeServiceBusReceiverWrapper _messageReceiver;
    private readonly FakeMessageProducer _fakeMessageProducer;
    private readonly FakeServiceBusReceiverProvider _fakeMessageReceiver;

    private readonly AzureServiceBusSubscriptionConfiguration _subConfig = new();

    public AzureServiceBusConsumerTestsAsync()
    {
        _nameSpaceManagerWrapper = new FakeAdministrationClient();
        _fakeMessageProducer = new FakeMessageProducer();
        _messageReceiver = new FakeServiceBusReceiverWrapper();
        _fakeMessageReceiver = new FakeServiceBusReceiverProvider(_messageReceiver);


        var sub = new AzureServiceBusSubscription<ASBTestCommand>(routingKey: new RoutingKey("topic"), channelName: new ChannelName("subscription")
            ,makeChannels: OnMissingChannel.Create, bufferSize: 10, subscriptionConfiguration: _subConfig);

        _azureServiceBusConsumer = new AzureServiceBusTopicConsumer(sub, _fakeMessageProducer,
            _nameSpaceManagerWrapper, _fakeMessageReceiver);
    }

    [Test]
    public async Task When_a_subscription_exists_and_messages_are_in_the_queue_the_messages_are_returned()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", ["subscription"]);

        var brokeredMessageList = new List<IBrokeredMessageWrapper>();
        var message1 = new BrokeredMessage()
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("somebody"),
            ApplicationProperties = new Dictionary<string, object> { { "MessageType", "MT_EVENT" } }
        };

        var message2 = new BrokeredMessage()
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("somebody2"),
            ApplicationProperties = new Dictionary<string, object> { { "MessageType", "MT_DOCUMENT" } }
        };

        brokeredMessageList.Add(message1);
        brokeredMessageList.Add(message2);

        _messageReceiver.MessageQueue = brokeredMessageList;

        Message[] result = await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        await Assert.That(result[0].Body.Value).IsEqualTo("somebody");
        await Assert.That(result[0].Header.Topic).IsEqualTo("topic");
        await Assert.That(result[0].Header.MessageType).IsEqualTo(MessageType.MT_EVENT);

        await Assert.That(result[1].Body.Value).IsEqualTo("somebody2");
        await Assert.That(result[1].Header.Topic).IsEqualTo("topic");
        await Assert.That(result[1].Header.MessageType).IsEqualTo(MessageType.MT_DOCUMENT);
    }

    [Test]
    public async Task When_a_subscription_does_not_exist_and_messages_are_in_the_queue_then_the_subscription_is_created_and_messages_are_returned()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", new ());
        var brokeredMessageList = new List<IBrokeredMessageWrapper>();
        var message1 = new BrokeredMessage()
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("somebody"),
            ApplicationProperties = new Dictionary<string, object> { { "MessageType", "MT_EVENT" } }
        };
        brokeredMessageList.Add(message1);

        _messageReceiver.MessageQueue = brokeredMessageList;

        Message[] result =await  _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        await _nameSpaceManagerWrapper.SubscriptionExistsAsync("topic", "subscription");
        await Assert.That(result[0].Body.Value).IsEqualTo("somebody");
    }

    [Test]
    public async Task When_a_message_is_a_command_type_then_the_message_type_is_set_correctly()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", ["subscription"]);

        var brokeredMessageList = new List<IBrokeredMessageWrapper>();
        var message1 = new BrokeredMessage()
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("somebody"),
            ApplicationProperties = new Dictionary<string, object> { { "MessageType", "MT_COMMAND" } }
        };
        brokeredMessageList.Add(message1);

        _messageReceiver.MessageQueue = brokeredMessageList;

        Message[] result =await  _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        await Assert.That(result[0].Body.Value).IsEqualTo("somebody");
        await Assert.That(result[0].Header.Topic).IsEqualTo("topic");
        await Assert.That(result[0].Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
    }

    [Test]
    public async Task When_a_message_is_a_command_type_and_it_is_specified_in_funny_casing_then_the_message_type_is_set_correctly()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", ["subscription"]);

        var brokeredMessageList = new List<IBrokeredMessageWrapper>();
        var message1 = new BrokeredMessage()
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("somebody"),
            ApplicationProperties = new Dictionary<string, object> { { "MessageType", "MT_COmmAND" } }
        };
        brokeredMessageList.Add(message1);

        _messageReceiver.MessageQueue = brokeredMessageList;

        Message[] result = await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        await Assert.That(result[0].Body.Value).IsEqualTo("somebody");
        await Assert.That(result[0].Header.Topic).IsEqualTo("topic");
        await Assert.That(result[0].Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
    }

    [Test]
    public async Task When_the_specified_message_type_is_unknown_then_it_should_default_to_MT_EVENT()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", ["subscription"]);

        var brokeredMessageList = new List<IBrokeredMessageWrapper>();
        var message1 = new BrokeredMessage()
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("somebody"),
            ApplicationProperties = new Dictionary<string, object> { { "MessageType", "wrong_message_type" } }
        };
        brokeredMessageList.Add(message1);

        _messageReceiver.MessageQueue = brokeredMessageList;

        Message[] result = await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        await Assert.That(result[0].Header.MessageType).IsEqualTo(MessageType.MT_EVENT);
    }

    [Test]
    public async Task When_the_message_type_is_not_specified_it_should_default_to_MT_EVENT()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", ["subscription"]);

        var brokeredMessageList = new List<IBrokeredMessageWrapper>();
        var message1 = new BrokeredMessage()
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("somebody"),
            ApplicationProperties = new Dictionary<string, object>()
        };
        brokeredMessageList.Add(message1);

        _messageReceiver.MessageQueue = brokeredMessageList;

        Message[] result = await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        await Assert.That(result[0].Body.Value).IsEqualTo("somebody");
        await Assert.That(result[0].Header.Topic).IsEqualTo("topic");
        await Assert.That(result[0].Header.MessageType).IsEqualTo(MessageType.MT_EVENT);
    }

    [Test]
    public async Task When_the_user_properties_on_the_azure_sb_message_is_null_it_should_default_to_message_type_to_MT_EVENT()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", ["subscription"]);


        var brokeredMessageList = new List<IBrokeredMessageWrapper>();
        var message1 = new BrokeredMessage()
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("somebody"),
            ApplicationProperties = new Dictionary<string, object>()
        };
        brokeredMessageList.Add(message1);

        _messageReceiver.MessageQueue = brokeredMessageList;

        Message[] result = await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        await Assert.That(result[0].Body.Value).IsEqualTo("somebody");
        await Assert.That(result[0].Header.Topic).IsEqualTo("topic");
        await Assert.That(result[0].Header.MessageType).IsEqualTo(MessageType.MT_EVENT);
    }

    [Test]
    public async Task When_there_are_no_messages_then_it_returns_an_empty_array()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", ["subscription"]);
        var brokeredMessageList = new List<IBrokeredMessageWrapper>();

        _messageReceiver.MessageQueue = brokeredMessageList;

        Message[] result = await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task When_trying_to_create_a_subscription_which_was_already_created_by_another_thread_it_should_ignore_the_error()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.CreateSubscriptionException =
            new ServiceBusException("whatever", ServiceBusFailureReason.MessagingEntityAlreadyExists);

        var brokeredMessageList = new List<IBrokeredMessageWrapper>();
        var message1 = new BrokeredMessage()
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("somebody"),
            ApplicationProperties = new Dictionary<string, object> { { "MessageType", "MT_EVENT" } }
        };
        brokeredMessageList.Add(message1);

        _messageReceiver.MessageQueue = brokeredMessageList;

        Message[] result = await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        await Assert.That(result[0].Body.Value).IsEqualTo("somebody");
    }

    [Test]
    public async Task When_dispose_is_called_the_close_method_is_called()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", new ());
        await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.Zero);
        await _azureServiceBusConsumer.DisposeAsync();

        await Assert.That(_messageReceiver.IsClosedOrClosing).IsTrue();
    }

    [Test]
    public async Task When_requeue_is_called_and_the_delay_is_zero_the_send_method_is_called()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", new ());
        _fakeMessageProducer.SentMessages.Clear();
        var messageLockTokenOne = Guid.NewGuid();
        var messageHeader = new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("topic"), MessageType.MT_EVENT);
        var message = new Message(messageHeader, new MessageBody("body"));
        message.Header.Bag.Add("LockToken", messageLockTokenOne);

        await _azureServiceBusConsumer.RequeueAsync(message, TimeSpan.Zero);

        await Assert.That(_fakeMessageProducer.SentMessages).HasSingleItem();
    }

    [Test]
    public async Task When_requeue_is_called_and_the_delay_is_more_than_zero_the_sendWithDelay_method_is_called()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", new ());
        _fakeMessageProducer.SentMessages.Clear();

        var messageLockTokenOne = Guid.NewGuid();
        var messageHeader = new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("topic"), MessageType.MT_EVENT);
        var message = new Message(messageHeader, new MessageBody("body"));
        message.Header.Bag.Add("LockToken", messageLockTokenOne);

        await _azureServiceBusConsumer.RequeueAsync(message, TimeSpan.FromMilliseconds(100));

        await Assert.That(_fakeMessageProducer.SentMessages).HasSingleItem();
    }

    [Test]
    public async Task When_there_is_an_error_talking_to_servicebus_when_checking_if_subscription_exist_then_a_ChannelFailureException_is_raised()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.CreateSubscriptionException = new Exception();

        await Assert.That(() => _azureServiceBusConsumer.Receive(TimeSpan.FromMilliseconds(400))).ThrowsExactly<ChannelFailureException>();
    }

    [Test]
    public async Task When_there_is_an_error_talking_to_servicebus_when_creating_the_subscription_then_a_ChannelFailureException_is_raised_and_ManagementClientWrapper_is_reinitilised()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.CreateSubscriptionException = new Exception();

        await Assert.That(() => _azureServiceBusConsumer.Receive(TimeSpan.FromMilliseconds(400))).ThrowsExactly<ChannelFailureException>();
        await Assert.That(_nameSpaceManagerWrapper.ResetCount).IsEqualTo(1);
    }

    /// <summary>
    /// TODO: review
    /// </summary>
    [Test]
    public async Task When_there_is_an_error_talking_to_servicebus_when_receiving_then_a_ChannelFailureException_is_raised_and_the_messageReceiver_is_recreated()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", ["subscription"]);

        _messageReceiver.MessageQueue.Clear();
        _messageReceiver.ReceiveException = new Exception();

        await Assert.That(() => _azureServiceBusConsumer.Receive(TimeSpan.FromMilliseconds(400))).ThrowsExactly<ChannelFailureException>();
        await Assert.That(_fakeMessageReceiver.CreationCount).IsEqualTo(2);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Once_the_subscription_is_created_or_exits_it_does_not_check_if_it_exists_every_time(bool subscriptionExists)
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", new ());
        _messageReceiver.MessageQueue.Clear();
        if (subscriptionExists) await _nameSpaceManagerWrapper.CreateSubscriptionAsync("topic", "subscription", new());
        var brokeredMessageList = new List<IBrokeredMessageWrapper>();
        var message1 = new BrokeredMessage()
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("somebody"),
            ApplicationProperties = new Dictionary<string, object> { { "MessageType", "MT_EVENT" } }
        };
        brokeredMessageList.Add(message1);

        _messageReceiver.MessageQueue = brokeredMessageList;

        await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));
        await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        //Subscription is only created once
        await Assert.That(_nameSpaceManagerWrapper.Topics["topic"].Count(s => s.Equals("subscription"))).IsEqualTo(1);

        await Assert.That(_nameSpaceManagerWrapper.ExistCount).IsEqualTo(1);
    }

    [Test]
    public async Task When_MessagingEntityAlreadyExistsException_does_not_check_if_subscription_exists()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", new ());
        _nameSpaceManagerWrapper.CreateSubscriptionException =
            new ServiceBusException("whatever", ServiceBusFailureReason.MessagingEntityAlreadyExists);
        _messageReceiver.MessageQueue.Clear();

        var brokeredMessageList = new List<IBrokeredMessageWrapper>();
        var message1 = new BrokeredMessage()
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("somebody"),
            ApplicationProperties = new Dictionary<string, object> { { "MessageType", "MT_EVENT" } }
        };
        brokeredMessageList.Add(message1);

        _messageReceiver.MessageQueue = brokeredMessageList;

        Message[] result = await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));
        await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        await Assert.That(result[0].Body.Value).IsEqualTo("somebody");

        await Assert.That(_nameSpaceManagerWrapper.ExistCount).IsEqualTo(1);
    }

    [Test]
    public async Task When_a_message_contains_a_null_body_message_is_still_processed()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", new ());

        _messageReceiver.MessageQueue.Clear();

        var brokeredMessageList = new List<IBrokeredMessageWrapper>();
        var message1 = new BrokeredMessage()
        {
            MessageBodyValue = null,
            ApplicationProperties = new Dictionary<string, object> { { "MessageType", "MT_EVENT" } }
        };

        brokeredMessageList.Add(message1);

        _messageReceiver.MessageQueue = brokeredMessageList;

        Message[] result = await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        await Assert.That(result[0].Body.Value).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task When_receiving_messages_and_the_receiver_is_closing_a_MT_QUIT_message_is_sent()
    {
        _nameSpaceManagerWrapper.Topics.Add("topic", new ());
        await _messageReceiver.CloseAsync();

        Message[] result = await _azureServiceBusConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(400));

        await Assert.That(result[0].Header.MessageType).IsEqualTo(MessageType.MT_QUIT);

    }

    [Test]
    public async Task When_rejecting_a_message_the_reason_and_description_are_forwarded_to_the_dead_letter_queue()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", ["subscription"]);

        var messageHeader = new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("topic"), MessageType.MT_EVENT);
        var message = new Message(messageHeader, new MessageBody("body"));
        message.Header.Bag.Add("LockToken", Guid.NewGuid());

        var reason = new MessageRejectionReason(RejectionReason.Unacceptable, "currency-missing");

        await _azureServiceBusConsumer.RejectAsync(message, reason);

        await Assert.That(_messageReceiver.DeadLetterReason).IsEqualTo("Unacceptable");
        await Assert.That(_messageReceiver.DeadLetterDescription).IsEqualTo("currency-missing");
    }

    [Test]
    public async Task When_rejecting_a_message_with_no_reason_a_default_reason_and_description_are_forwarded_to_the_dead_letter_queue()
    {
        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", ["subscription"]);

        var messageHeader = new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("topic"), MessageType.MT_EVENT);
        var message = new Message(messageHeader, new MessageBody("body"));
        message.Header.Bag.Add("LockToken", Guid.NewGuid());

        await _azureServiceBusConsumer.RejectAsync(message);

        await Assert.That(_messageReceiver.DeadLetterReason).IsEqualTo("DeliveryError");
        await Assert.That(_messageReceiver.DeadLetterDescription).IsEqualTo("unknown");
    }

    [Test]
    public async Task When_a_subscription_does_not_exist_and_Missing_is_set_to_Validate_a_Channel_Failure_is_Raised()
    {
        _nameSpaceManagerWrapper.ResetState();

        var sub = new AzureServiceBusSubscription<ASBTestCommand>(routingKey: new RoutingKey("topic"), channelName: new ChannelName("subscription")
            ,makeChannels: OnMissingChannel.Validate, subscriptionConfiguration: _subConfig);

        var azureServiceBusConsumerValidate = new AzureServiceBusTopicConsumer(sub, _fakeMessageProducer,
            _nameSpaceManagerWrapper, _fakeMessageReceiver);

        await Assert.That(() => azureServiceBusConsumerValidate.Receive(TimeSpan.FromMilliseconds(400))).ThrowsExactly<ChannelFailureException>();
    }
}
