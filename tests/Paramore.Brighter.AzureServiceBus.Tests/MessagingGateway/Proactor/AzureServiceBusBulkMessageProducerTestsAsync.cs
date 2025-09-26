using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway.Proactor;

public class AzureServiceBusBulkMessageProducerTestsAsync
{
    private readonly FakeAdministrationClient _nameSpaceManagerWrapper;
    private readonly AzureServiceBusMessageProducer _producer;
    private readonly AzureServiceBusMessageProducer _queueProducer;
    private readonly FakeServiceBusSenderWrapper _topicClient;

    public AzureServiceBusBulkMessageProducerTestsAsync()
    {
        _nameSpaceManagerWrapper = new FakeAdministrationClient();
        _topicClient = new FakeServiceBusSenderWrapper();
        FakeServiceBusSenderProvider topicClientProvider = new(_topicClient);


        _producer = new AzureServiceBusTopicMessageProducer(
            _nameSpaceManagerWrapper,
            topicClientProvider,
            new AzureServiceBusPublication { MakeChannels = OnMissingChannel.Create }
        );

        _queueProducer = new AzureServiceBusQueueMessageProducer(
            _nameSpaceManagerWrapper,
            topicClientProvider,
            new AzureServiceBusPublication { MakeChannels = OnMissingChannel.Create }
        );
    }

    [Fact]
    public async Task
        When_the_topic_exists_and_sending_a_batch_with_one_message_it_should_send_the_message_to_the_correct_topicclient()
    {
        byte[] messageBody = Encoding.UTF8.GetBytes("A message body");
        List<Message> messages = new()
        {
            new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_EVENT),
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))
            )
        };

        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", []);

        IEnumerable<IAmAMessageBatch> batches = await _producer.CreateBatchesAsync(messages, default);

        foreach (IAmAMessageBatch batch in batches)
            await _producer.SendAsync(batch, default);

        ServiceBusMessage sentMessage = _topicClient.SentMessages.Single();

        Assert.Equal(messageBody, sentMessage.Body.ToArray());
        Assert.Equal("MT_EVENT", sentMessage.ApplicationProperties["MessageType"]);
        Assert.Equal(2, _topicClient.ClosedCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_sending_a_command_message_type_message_it_should_set_the_correct_messagetype_property(
        bool useQueues)
    {
        byte[] messageBody = Encoding.UTF8.GetBytes("A message body");
        List<Message> messages = new()
        {
            new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_COMMAND),
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))
            )
        };

        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", []);
        _nameSpaceManagerWrapper.Queues.Add("topic");

        AzureServiceBusMessageProducer producer = useQueues ? _queueProducer : _producer;
        IEnumerable<IAmAMessageBatch> batches = await producer.CreateBatchesAsync(messages, default);
        foreach (IAmAMessageBatch batch in batches)
            await producer.SendAsync(batch, default);

        ServiceBusMessage sentMessage = _topicClient.SentMessages.Single();

        Assert.Equal(messageBody, sentMessage.Body.ToArray());
        Assert.Equal("MT_COMMAND", sentMessage.ApplicationProperties["MessageType"]);
        Assert.Equal(2, _topicClient.ClosedCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task
        When_the_topic_does_not_exist_it_should_be_created_and_the_message_is_sent_to_the_correct_topicclient(
            bool useQueues)
    {
        byte[] messageBody = Encoding.UTF8.GetBytes("A message body");
        List<Message> messages = new()
        {
            new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE),
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))
            )
        };

        _nameSpaceManagerWrapper.ResetState();

        AzureServiceBusMessageProducer producer = useQueues ? _queueProducer : _producer;
        IEnumerable<IAmAMessageBatch> batches = await producer.CreateBatchesAsync(messages, default);
        foreach (IAmAMessageBatch batch in batches)
            await producer.SendAsync(batch, default);

        ServiceBusMessage sentMessage = _topicClient.SentMessages.Single();

        Assert.Equal(1, _nameSpaceManagerWrapper.CreateCount);
        Assert.Equal(messageBody, sentMessage.Body.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_a_message_batch_is_created_and_an_exception_occurs_close_is_still_called(bool useQueues)
    {
        byte[] messageBody = Encoding.UTF8.GetBytes("A message body");
        List<Message> messages = new()
        {
            new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE),
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))
            )
        };

        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", []);
        _nameSpaceManagerWrapper.Queues.Add("topic");

        _topicClient.SendException = new Exception("Failed");

        try
        {
            AzureServiceBusMessageProducer producer = useQueues ? _queueProducer : _producer;
            IEnumerable<IAmAMessageBatch> batches = await producer.CreateBatchesAsync(messages, default);
            foreach (IAmAMessageBatch batch in batches)
                await producer.SendAsync(batch, default);
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
    public async Task When_a_message_batch_is_send_and_an_exception_occurs_close_is_still_called(bool useQueues)
    {
        byte[] messageBody = Encoding.UTF8.GetBytes("A message body");
        List<Message> messages = new()
        {
            new Message(
                new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_NONE),
                new MessageBody(messageBody, new ContentType(MediaTypeNames.Application.Json))
            )
        };

        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", []);
        _nameSpaceManagerWrapper.Queues.Add("topic");

        try
        {
            AzureServiceBusMessageProducer producer = useQueues ? _queueProducer : _producer;
            IEnumerable<IAmAMessageBatch> batches = await producer.CreateBatchesAsync(messages, default);
            _topicClient.SendException = new Exception("Failed");
            foreach (IAmAMessageBatch batch in batches)
                await producer.SendAsync(batch, default);
        }
        catch (Exception)
        {
            // ignored
        }

        Assert.Equal(2, _topicClient.ClosedCount);
    }

    [Fact]
    public async Task
        When_a_message_batch_is_created_for_a_couple_message_that_exceeds_message_batch_size_a_new_batch_is_created()
    {
        int tryAddcallBackCount = 0;
        Message message1 = new(
            new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_COMMAND),
            new MessageBody(Encoding.UTF8.GetBytes("A message body"), new ContentType(MediaTypeNames.Application.Json))
        );
        Message message2 = new(
            new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_COMMAND),
            new MessageBody(Encoding.UTF8.GetBytes("A message body"), new ContentType(MediaTypeNames.Application.Json))
        );
        _topicClient.TryAddMessageCallBack = _ =>
        {
            tryAddcallBackCount++;
            if (tryAddcallBackCount % 2 == 0)
                return false;

            return true;
        };

        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", []);

        IEnumerable<IAmAMessageBatch> batches = await _producer.CreateBatchesAsync([message1, message2], default);
        foreach (IAmAMessageBatch batch in batches)
            await _producer.SendAsync(batch, default);

        ServiceBusMessage sentMessage1 = _topicClient.SentMessages.Single(x => x.MessageId == message1.Id.Value);
        ServiceBusMessage sentMessage2 = _topicClient.SentMessages.Single(x => x.MessageId == message2.Id.Value);

        Assert.Equal(2, batches.Count());
        Assert.Equal(message1.Body.Value, sentMessage1.Body.ToString());
        Assert.Equal("MT_COMMAND", sentMessage1.ApplicationProperties["MessageType"]);
        Assert.Equal(message2.Body.Value, sentMessage2.Body.ToString());
        Assert.Equal("MT_COMMAND", sentMessage2.ApplicationProperties["MessageType"]);
    }

    [Fact]
    public async Task
        When_a_message_batch_is_created_for_a_single_message_that_exceeds_message_batch_size_a_single_message_batch_is_created()
    {
        Message message1 = new(
            new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_COMMAND),
            new MessageBody(Encoding.UTF8.GetBytes("A message body"), new ContentType(MediaTypeNames.Application.Json))
        );
        _topicClient.TryAddMessageCallBack = _ => false;

        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", []);

        IEnumerable<IAmAMessageBatch> batches = await _producer.CreateBatchesAsync(new[] { message1 }, default);
        foreach (IAmAMessageBatch batch in batches)
            await _producer.SendAsync(batch, default);

        ServiceBusMessage sentMessage1 = _topicClient.SentMessages.Single(x => x.MessageId == message1.Id.Value);
        AzureServiceBusSingleMessageBatch? singleMessageBatch = batches.First() as AzureServiceBusSingleMessageBatch;

        Assert.Single(batches);
        Assert.True(singleMessageBatch is not null);
        Assert.Equal(message1.Body.Value, sentMessage1.Body.ToString());
        Assert.Equal("MT_COMMAND", sentMessage1.ApplicationProperties["MessageType"]);
    }

    [Fact]
    public async Task
        When_a_message_batch_is_created_for_a_few_messages_with_1_that_exceeds_message_batch_size_a_new_batch_is_created()
    {
        int tryMessage2CallBack = 0;
        Message message1 = new(
            new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_COMMAND),
            new MessageBody(Encoding.UTF8.GetBytes("A message body"), new ContentType(MediaTypeNames.Application.Json))
        );
        Message message2 = new(
            new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_COMMAND),
            new MessageBody(Encoding.UTF8.GetBytes("A message body"), new ContentType(MediaTypeNames.Application.Json))
        );
        Message message3 = new(
            new MessageHeader(Id.Random(), new RoutingKey("topic"), MessageType.MT_COMMAND),
            new MessageBody(Encoding.UTF8.GetBytes("A message body"), new ContentType(MediaTypeNames.Application.Json))
        );
        _topicClient.TryAddMessageCallBack = servicebusmessage =>
        {
            if (servicebusmessage.MessageId == message3.Id)
                return false;
            return true;
        };

        _nameSpaceManagerWrapper.ResetState();
        _nameSpaceManagerWrapper.Topics.Add("topic", []);

        IEnumerable<IAmAMessageBatch> batches =
            await _producer.CreateBatchesAsync(new[] { message1, message2, message3 }, default);
        foreach (IAmAMessageBatch batch in batches)
            await _producer.SendAsync(batch, default);

        ServiceBusMessage sentMessage1 = _topicClient.SentMessages.Single(x => x.MessageId == message1.Id.Value);
        ServiceBusMessage sentMessage2 = _topicClient.SentMessages.Single(x => x.MessageId == message2.Id.Value);
        ServiceBusMessage sentMessage3 = _topicClient.SentMessages.Single(x => x.MessageId == message3.Id.Value);

        Assert.Equal(2, batches.Count());
        Assert.Equal(message1.Body.Value, sentMessage1.Body.ToString());
        Assert.Equal("MT_COMMAND", sentMessage1.ApplicationProperties["MessageType"]);
        Assert.Equal(message2.Body.Value, sentMessage2.Body.ToString());
        Assert.Equal("MT_COMMAND", sentMessage2.ApplicationProperties["MessageType"]);
        Assert.Equal(message3.Body.Value, sentMessage3.Body.ToString());
        Assert.Equal("MT_COMMAND", sentMessage3.ApplicationProperties["MessageType"]);
    }
}
