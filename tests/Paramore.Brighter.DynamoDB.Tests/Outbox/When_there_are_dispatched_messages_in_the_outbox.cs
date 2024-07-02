using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

[Trait("Category", "DynamoDB")]
public class DynamoDbOutboxDispatchedMessageTests : DynamoDBOutboxBaseTest
{
    private readonly Message _message;
    private readonly DynamoDbOutbox _dynamoDbOutbox;

    public DynamoDbOutboxDispatchedMessageTests()
    {
        _message = CreateMessage("test_topic");
        _dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(OutboxTableName));
    }

    [Fact]
    public async Task When_there_are_dispatched_messages_in_the_outbox_async()
    {
        var context = new RequestContext();
        await _dynamoDbOutbox.AddAsync(_message, context);
        await _dynamoDbOutbox.MarkDispatchedAsync(_message.Id, context);

        await Task.Delay(1000);

        var args = new Dictionary<string, object> {{"Topic", "test_topic"}};

        var messages = await _dynamoDbOutbox.DispatchedMessagesAsync(0, context, 100, 1, args: args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Value.Should().Be(_message.Body.Value);
    }

    [Fact]
    public async Task When_there_are_dispatched_messages_in_the_outbox()
    {
        var context = new RequestContext();
        _dynamoDbOutbox.Add(_message, context);
        _dynamoDbOutbox.MarkDispatched(_message.Id, context);

        await Task.Delay(1000);

        var args = new Dictionary<string, object> {{"Topic", "test_topic"}};

        var messages = _dynamoDbOutbox.DispatchedMessages(0, context, 100, 1, args: args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Value.Should().Be(_message.Body.Value);
    }

    [Fact]
    public async Task When_there_are_dispatched_messages_for_multiple_topics_async()
    {
        var messages = new List<Message>();
        var context = new RequestContext();
        messages.Add(CreateMessage("one_topic"));
        messages.Add(CreateMessage("another_topic"));

        foreach (var message in messages)
        {
            await _dynamoDbOutbox.AddAsync(message, context);
            await _dynamoDbOutbox.MarkDispatchedAsync(message.Id, context);
        }

        await Task.Delay(1000);

        var dispatchedMessages = await _dynamoDbOutbox.DispatchedMessagesAsync(0, context, 100, 1);

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var dispatchedMessage = dispatchedMessages.Single(m => m.Id == message.Id);
            dispatchedMessage.Should().NotBeNull();
            dispatchedMessage.Body.Value.Should().Be(message.Body.Value);
            dispatchedMessage.Header.Topic.Should().Be(message.Header.Topic);
        }
    }

    [Fact]
    public async Task When_there_are_dispatched_messages_for_multiple_topics()
    {
        var messages = new List<Message>();
        var context = new RequestContext();
        messages.Add(CreateMessage("one_topic"));
        messages.Add(CreateMessage("another_topic"));

        foreach (var message in messages)
        {
            _dynamoDbOutbox.Add(message, context);
            _dynamoDbOutbox.MarkDispatched(message.Id, context);
        }

        await Task.Delay(1000);

        var dispatchedMessages = _dynamoDbOutbox.DispatchedMessages(0, context, 100, 1);

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var dispatchedMessage = dispatchedMessages.Single(m => m.Id == message.Id);
            dispatchedMessage.Should().NotBeNull();
            dispatchedMessage.Body.Value.Should().Be(message.Body.Value);
            dispatchedMessage.Header.Topic.Should().Be(message.Header.Topic);
        }
    }

    [Fact]
    public async Task When_there_are_multiple_pages_of_dispatched_messages_for_a_topic_async()
    {
        var context = new RequestContext();
        var messages = new List<Message>();
        // Create enough messages to guarantee they will be split across multiple shards
        for (var i = 0; i < 10; i++)
        {
            messages.Add(CreateMessage("test_topic"));
        }

        foreach (var message in messages)
        {
            await _dynamoDbOutbox.AddAsync(message, context);
            await _dynamoDbOutbox.MarkDispatchedAsync(message.Id, context);
        }

        await Task.Delay(1000);

        var args = new Dictionary<string, object> { { "Topic", "test_topic" } };

        // Get the first page
        var dispatchedMessages = (await _dynamoDbOutbox.DispatchedMessagesAsync(0, context, 5, 1, args: args)).ToList();
        dispatchedMessages.Count.Should().Be(5);
        // Get the remainder
        dispatchedMessages.AddRange(await _dynamoDbOutbox.DispatchedMessagesAsync(0, context, 100, 2, args: args));

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var dispatchedMessage = dispatchedMessages.Single(m => m.Id == message.Id);
            dispatchedMessage.Should().NotBeNull();
            dispatchedMessage.Body.Value.Should().Be(message.Body.Value);
        }
    }

    [Fact]
    public async Task When_there_are_multiple_pages_of_dispatched_messages_for_a_topic()
    {
        var context = new RequestContext();
        var messages = new List<Message>();
        // Create enough messages to guarantee they will be split across multiple shards
        for (var i = 0; i < 10; i++)
        {
            messages.Add(CreateMessage("test_topic"));
        }

        foreach (var message in messages)
        {
            _dynamoDbOutbox.Add(message, context);
            _dynamoDbOutbox.MarkDispatched(message.Id, context);
        }

        await Task.Delay(1000);

        var args = new Dictionary<string, object> { { "Topic", "test_topic" } };

        // Get the first page
        var dispatchedMessages = (_dynamoDbOutbox.DispatchedMessages(0, context, 5, 1, args: args)).ToList();
        dispatchedMessages.Count.Should().Be(5);
        // Get the remainder
        dispatchedMessages.AddRange(_dynamoDbOutbox.DispatchedMessages(0, context, 100, 2, args: args));

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var dispatchedMessage = dispatchedMessages.Single(m => m.Id == message.Id);
            dispatchedMessage.Should().NotBeNull();
            dispatchedMessage.Body.Value.Should().Be(message.Body.Value);
        }
    }

    [Fact]
    public async Task When_there_are_multiple_pages_of_dispatched_messages_for_all_topics_async()
    {
        var context = new RequestContext();
        var messages = new List<Message>();

        // Create enough messages to guarantee they will be split across multiple shards
        // for all topics
        var topics = new[] { "one_topic", "another_topic" };
        foreach (var topic in topics)
        {
            for (var i = 0; i < 10; i++)
            {
                messages.Add(CreateMessage(topic));
            }
        }

        foreach (var message in messages)
        {
            await _dynamoDbOutbox.AddAsync(message, context);
            await _dynamoDbOutbox.MarkDispatchedAsync(message.Id, context);
        }

        await Task.Delay(1000);

        // Get the messages over 4 pages
        var dispatchedMessages = new List<Message>();
        for (var i = 1; i < 5; i++)
        {
            dispatchedMessages.AddRange(await _dynamoDbOutbox.DispatchedMessagesAsync(0, context, 5, i));
        }
        // Do a last page in case other tests have added more messages
        dispatchedMessages.AddRange(await _dynamoDbOutbox.DispatchedMessagesAsync(0, context, 100, 5));

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var dispatchedMessage = dispatchedMessages.Single(m => m.Id == message.Id);
            dispatchedMessage.Should().NotBeNull();
            dispatchedMessage.Body.Value.Should().Be(message.Body.Value);
            dispatchedMessage.Header.Topic.Should().Be(message.Header.Topic);
        }
    }

    [Fact]
    public async Task When_there_are_multiple_pages_of_dispatched_messages_for_all_topics()
    {
        var context = new RequestContext();
        var messages = new List<Message>();

        // Create enough messages to guarantee they will be split across multiple shards
        // for all topics
        var topics = new[] { "one_topic", "another_topic" };
        foreach (var topic in topics)
        {
            for (var i = 0; i < 10; i++)
            {
                messages.Add(CreateMessage(topic));
            }
        }

        foreach (var message in messages)
        {
            _dynamoDbOutbox.Add(message, context);
            _dynamoDbOutbox.MarkDispatched(message.Id, context);
        }

        await Task.Delay(1000);

        // Get the messages over 4 pages
        var dispatchedMessages = new List<Message>();
        for (var i = 1; i < 5; i++)
        {
            dispatchedMessages.AddRange(_dynamoDbOutbox.DispatchedMessages(0, context, 5, i));
        }
        // Do a last page in case other tests have added more messages
        dispatchedMessages.AddRange(_dynamoDbOutbox.DispatchedMessages(0, context, 100, 5));

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var dispatchesMessage = dispatchedMessages.Single(m => m.Id == message.Id);
            dispatchesMessage.Should().NotBeNull();
            dispatchesMessage.Body.Value.Should().Be(message.Body.Value);
            dispatchesMessage.Header.Topic.Should().Be(message.Header.Topic);
        }
    }

    private Message CreateMessage(string topic)
    {
        return new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_DOCUMENT),
            new MessageBody("message body")
        );
    }
}
