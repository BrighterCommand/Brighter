using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using FluentAssertions;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

[Trait("Category", "DynamoDB")]
public class DynamoDbOutboxOutstandingMessageTests : DynamoDBOutboxBaseTest
{
    private readonly Message _message;
    private readonly DynamoDbOutbox _dynamoDbOutbox;

    public DynamoDbOutboxOutstandingMessageTests()
    {
        _message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
        _dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(Credentials, RegionEndpoint.EUWest1, OutboxTableName));
    }

    [Fact]
    public async Task When_there_are_outstanding_messages_in_the_outbox_async()
    {
        await _dynamoDbOutbox.AddAsync(_message);

        await Task.Delay(1000);

        var args = new Dictionary<string, object> {{"Topic", "test_topic"}};

        var messages = await _dynamoDbOutbox.OutstandingMessagesAsync(0, 100, 1, args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Should().Be(_message.Body);
    }

    [Fact]
    public async Task When_there_are_outstanding_messages_in_the_outbox()
    {
        _dynamoDbOutbox.Add(_message);

        await Task.Delay(1000);

        var args = new Dictionary<string, object> {{"Topic", "test_topic"}};

        var messages =_dynamoDbOutbox.OutstandingMessages(0, 100, 1, args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();
        message.Body.Should().Be(_message.Body);
    }

    [Fact]
    public async Task When_there_are_outstanding_messages_for_multiple_topics_async()
    {
        var messages = new List<Message>();
        messages.Add(CreateMessage("one_topic"));
        messages.Add(CreateMessage("another_topic"));

        foreach (var message in messages)
        {
            await _dynamoDbOutbox.AddAsync(message);
        }

        await Task.Delay(1000);

        var outstandingMessages = await _dynamoDbOutbox.OutstandingMessagesAsync(0, 100, 1);

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var outstandingMessage = outstandingMessages.Single(m => m.Id == message.Id);
            outstandingMessage.Should().NotBeNull();
            outstandingMessage.Body.Value.Should().Be(message.Body.Value);
            outstandingMessage.Header.Topic.Should().Be(message.Header.Topic);
        }
    }

    [Fact]
    public async Task When_there_are_outstanding_messages_for_multiple_topics()
    {
        var messages = new List<Message>();
        messages.Add(CreateMessage("one_topic"));
        messages.Add(CreateMessage("another_topic"));

        foreach (var message in messages)
        {
            _dynamoDbOutbox.Add(message);
        }

        await Task.Delay(1000);

        var outstandingMessages = _dynamoDbOutbox.OutstandingMessages(0, 100, 1);

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var outstandingMessage = outstandingMessages.Single(m => m.Id == message.Id);
            outstandingMessage.Should().NotBeNull();
            outstandingMessage.Body.Value.Should().Be(message.Body.Value);
            outstandingMessage.Header.Topic.Should().Be(message.Header.Topic);
        }
    }

    [Fact]
    public async Task When_an_outstanding_message_is_dispatched_async()
    {
        await _dynamoDbOutbox.AddAsync(_message);

        await Task.Delay(1000);

        var args = new Dictionary<string, object> { { "Topic", "test_topic" } };

        var messages = await _dynamoDbOutbox.OutstandingMessagesAsync(0, 100, 1, args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();

        await _dynamoDbOutbox.MarkDispatchedAsync(_message.Id);

        // Give the GSI a second to catch up
        await Task.Delay(1000);

        messages = await _dynamoDbOutbox.OutstandingMessagesAsync(0, 100, 1, args);
        messages.All(m => m.Id != _message.Id);
    }

    [Fact]
    public async Task When_an_outstanding_message_is_dispatched()
    {
        _dynamoDbOutbox.Add(_message);

        await Task.Delay(1000);

        var args = new Dictionary<string, object> { { "Topic", "test_topic" } };

        var messages = _dynamoDbOutbox.OutstandingMessages(0, 100, 1, args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        message.Should().NotBeNull();

        _dynamoDbOutbox.MarkDispatched(_message.Id);

        // Give the GSI a second to catch up
        await Task.Delay(1000);

        messages = _dynamoDbOutbox.OutstandingMessages(0, 100, 1, args);
        messages.All(m => m.Id != _message.Id);
    }

    private Message CreateMessage(string topic)
    {
        return new Message(
            new MessageHeader(Guid.NewGuid(), topic, MessageType.MT_DOCUMENT),
            new MessageBody("message body")
        );
    }
}
