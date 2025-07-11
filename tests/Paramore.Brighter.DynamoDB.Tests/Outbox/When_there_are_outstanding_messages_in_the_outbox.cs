using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

[Trait("Category", "DynamoDB")]
public class DynamoDbOutboxOutstandingMessageTests : DynamoDBOutboxBaseTest
{
    private readonly Message _message;
    private readonly DynamoDbOutbox _dynamoDbOutbox;
    private readonly FakeTimeProvider _fakeTimeProvider;

    public DynamoDbOutboxOutstandingMessageTests()
    {
        _fakeTimeProvider = new FakeTimeProvider();
        _message = CreateMessage("test_topic");
        _dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(OutboxTableName), _fakeTimeProvider);
    }

    [Fact]
    public async Task When_there_are_outstanding_messages_in_the_outbox_async()
    {
        var context = new RequestContext();
        await _dynamoDbOutbox.AddAsync(_message, context);

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        var args = new Dictionary<string, object> {{"Topic", "test_topic"}};

        var messages = await _dynamoDbOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context, 100, 1, args: args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        Assert.NotNull(message);
        Assert.Equal(_message.Body.Value, message.Body.Value);
    }

    [Fact]
    public void When_there_are_outstanding_messages_in_the_outbox()
    {
        var context = new RequestContext();
        _dynamoDbOutbox.Add(_message, context);

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        var args = new Dictionary<string, object> {{"Topic", "test_topic"}};

        var messages =_dynamoDbOutbox.OutstandingMessages(TimeSpan.Zero, context, 100, 1, args: args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        Assert.NotNull(message);
        Assert.Equal(_message.Body.Value, message.Body.Value);
    }

    [Fact]
    public async Task When_there_are_outstanding_messages_for_multiple_topics_async()
    {
        var messages = new List<Message>();
        var context = new RequestContext();
        messages.Add(CreateMessage("one_topic"));
        messages.Add(CreateMessage("another_topic"));

        foreach (var message in messages)
        {
            await _dynamoDbOutbox.AddAsync(message, context);
        }

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        var outstandingMessages = await _dynamoDbOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context, 100, 1);

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var outstandingMessage = outstandingMessages.Single(m => m.Id == message.Id);
            Assert.NotNull(outstandingMessage);
            Assert.Equal(message.Body.Value, outstandingMessage.Body.Value);
            Assert.Equal(message.Header.Topic, outstandingMessage.Header.Topic);
        }
    }

    [Fact]
    public void When_there_are_outstanding_messages_for_multiple_topics()
    {
        var messages = new List<Message>();
        var context = new RequestContext();
        messages.Add(CreateMessage("one_topic"));
        messages.Add(CreateMessage("another_topic"));

        foreach (var message in messages)
        {
            _dynamoDbOutbox.Add(message, context);
        }

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        var outstandingMessages = _dynamoDbOutbox.OutstandingMessages(TimeSpan.Zero, context, 100, 1);

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var outstandingMessage = outstandingMessages.Single(m => m.Id == message.Id);
            Assert.NotNull(outstandingMessage);
            Assert.Equal(message.Body.Value, outstandingMessage.Body.Value);
            Assert.Equal(message.Header.Topic, outstandingMessage.Header.Topic);
        }
    }

    [Fact]
    public async Task When_there_are_multiple_pages_of_outstanding_messages_for_a_topic_async()
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
        }

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        var args = new Dictionary<string, object> { { "Topic", "test_topic" } };

        // Get the first page
        var outstandingMessages = (await _dynamoDbOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context, 5, 1, args: args)).ToList();
        Assert.Equal(5, outstandingMessages.Count);
        // Get the remainder
        outstandingMessages.AddRange(await _dynamoDbOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context, 100, 2, args: args));

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var outstandingMessage = outstandingMessages.Single(m => m.Id == message.Id);
            Assert.NotNull(outstandingMessage);
            Assert.Equal(message.Body.Value, outstandingMessage.Body.Value);
        }
    }

    [Fact]
    public void When_there_are_multiple_pages_of_outstanding_messages_for_a_topic()
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
        }

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        var args = new Dictionary<string, object> { { "Topic", "test_topic" } };

        // Get the first page
        var outstandingMessages = _dynamoDbOutbox.OutstandingMessages(TimeSpan.Zero, context, 5, 1, args: args).ToList();
        Assert.Equal(5, outstandingMessages.Count);
        // Get the remainder
        outstandingMessages.AddRange(_dynamoDbOutbox.OutstandingMessages(TimeSpan.Zero, context, 100, 2, args: args));

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var outstandingMessage = outstandingMessages.Single(m => m.Id == message.Id);
            Assert.NotNull(outstandingMessage);
            Assert.Equal(message.Body.Value, outstandingMessage.Body.Value);
        }
    }

    [Fact]
    public async Task When_there_are_multiple_pages_of_outstanding_messages_for_all_topics_async()
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
        }

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        // Get the messages over 4 pages
        var outstandingMessages = new List<Message>();
        for (var i = 1; i < 5; i++)
        {
            outstandingMessages.AddRange(await _dynamoDbOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context, 5, i));
        }
        // Do a last page in case other tests have added more messages
        outstandingMessages.AddRange(await _dynamoDbOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context, 100, 5));

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var outstandingMessage = outstandingMessages.Single(m => m.Id == message.Id);
            Assert.NotNull(outstandingMessage);
            Assert.Equal(message.Body.Value, outstandingMessage.Body.Value);
            Assert.Equal(message.Header.Topic, outstandingMessage.Header.Topic);
        }
    }

    [Fact]
    public void When_there_are_multiple_pages_of_outstanding_messages_for_all_topics()
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
        }

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        // Get the messages over 4 pages
        var outstandingMessages = new List<Message>();
        for (var i = 1; i < 5; i++)
        {
            outstandingMessages.AddRange(_dynamoDbOutbox.OutstandingMessages(TimeSpan.Zero, context, 5, i));
        }
        // Do a last page in case other tests have added more messages
        outstandingMessages.AddRange(_dynamoDbOutbox.OutstandingMessages(TimeSpan.Zero, context, 100, 5));

        //Other tests may leave messages, so make sure that we grab ours
        foreach (var message in messages)
        {
            var outstandingMessage = outstandingMessages.Single(m => m.Id == message.Id);
            Assert.NotNull(outstandingMessage);
            Assert.Equal(message.Body.Value, outstandingMessage.Body.Value);
            Assert.Equal(message.Header.Topic, outstandingMessage.Header.Topic);
        }
    }

    [Fact]
    public async Task When_an_outstanding_message_is_dispatched_async()
    {
        var context = new RequestContext();
        await _dynamoDbOutbox.AddAsync(_message, context);

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        var args = new Dictionary<string, object> { { "Topic", "test_topic" } };

        var messages = await _dynamoDbOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context, 100, 1, args: args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        Assert.NotNull(message);

        await _dynamoDbOutbox.MarkDispatchedAsync(_message.Id, context);

        // Give the GSI a second to catch up
        await Task.Delay(1000);

        messages = await _dynamoDbOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context, 100, 1, args: args);
        messages.All(m => m.Id != _message.Id);
    }

    [Fact]
    public async Task When_an_outstanding_message_is_dispatched()
    {
        var context = new RequestContext(); 
        _dynamoDbOutbox.Add(_message, context);

        _fakeTimeProvider.Advance(TimeSpan.FromSeconds(1));

        var args = new Dictionary<string, object> { { "Topic", "test_topic" } };

        var messages = _dynamoDbOutbox.OutstandingMessages(TimeSpan.Zero, context, 100, 1, args: args);

        //Other tests may leave messages, so make sure that we grab ours
        var message = messages.Single(m => m.Id == _message.Id);
        Assert.NotNull(message);

        _dynamoDbOutbox.MarkDispatched(_message.Id, context);

        // Give the GSI a second to catch up
        await Task.Delay(1000);

        messages = _dynamoDbOutbox.OutstandingMessages(TimeSpan.Zero, context, 100, 1, args: args);
        messages.All(m => m.Id != _message.Id);
    }

    private Message CreateMessage(string topic)
    {
        return new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(topic), MessageType.MT_DOCUMENT, 
                timeStamp: _fakeTimeProvider.GetUtcNow().DateTime),
            new MessageBody("message body")
        );
    }
}
