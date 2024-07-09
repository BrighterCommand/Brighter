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
        await _dynamoDbOutbox.AddAsync(_message);
        await _dynamoDbOutbox.MarkDispatchedAsync(_message.Id);

        var args = new Dictionary<string, object> { { "Topic", "test_topic" } };

        var messages = await _dynamoDbOutbox.DispatchedMessagesAsync(0, 100, 1, args: args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Value.Should().Be(_message.Body.Value);
    }

    [Fact]
    public async Task When_there_are_dispatched_messages_in_the_outbox()
    {
        _dynamoDbOutbox.Add(_message);
        _dynamoDbOutbox.MarkDispatched(_message.Id);

        await Task.Delay(1000);

        var args = new Dictionary<string, object> { { "Topic", "test_topic" } };

        var messages = _dynamoDbOutbox.DispatchedMessages(0, 100, 1, args: args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Value.Should().Be(_message.Body.Value);
    }

    [Fact]
    public async Task When_there_are_dispatched_messages_for_multiple_topics_async()
    {
        var messages = new List<Message>();
        messages.Add(CreateMessage("one_topic"));
        messages.Add(CreateMessage("another_topic"));

        foreach (var message in messages)
        {
            await _dynamoDbOutbox.AddAsync(message);
            await _dynamoDbOutbox.MarkDispatchedAsync(message.Id);
        }

        await Task.Delay(1000);

        var dispatchedMessages = await _dynamoDbOutbox.DispatchedMessagesAsync(0, 100, 1);

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
        messages.Add(CreateMessage("one_topic"));
        messages.Add(CreateMessage("another_topic"));

        foreach (var message in messages)
        {
            _dynamoDbOutbox.Add(message);
            _dynamoDbOutbox.MarkDispatched(message.Id);
        }

        await Task.Delay(1000);

        var dispatchedMessages = _dynamoDbOutbox.DispatchedMessages(0, 100, 1);

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var dispatchedMessage = dispatchedMessages.Single(m => m.Id == message.Id);
            dispatchedMessage.Should().NotBeNull();
            dispatchedMessage.Body.Value.Should().Be(message.Body.Value);
            dispatchedMessage.Header.Topic.Should().Be(message.Header.Topic);
        }
    }

    private Message CreateMessage(string topic)
    {
        return new Message(
            new MessageHeader(Guid.NewGuid(), topic, MessageType.MT_DOCUMENT),
            new MessageBody("message body")
        );
    }
}
