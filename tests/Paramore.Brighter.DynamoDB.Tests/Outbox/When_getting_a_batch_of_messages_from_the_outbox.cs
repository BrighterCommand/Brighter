using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

[Trait("Category", "DynamoDB")]
public class DynamoDbOutboxBatchGetTests : DynamoDBOutboxBaseTest
{
    private readonly DynamoDbOutbox _dynamoDbOutbox;
    private readonly FakeTimeProvider _fakeTimeProvider;

    public DynamoDbOutboxBatchGetTests()
    {
        _fakeTimeProvider = new FakeTimeProvider();
        _dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(OutboxTableName), _fakeTimeProvider);
    }

    [Fact]
    public void When_getting_a_batch_of_messages_from_the_outbox()
    {
        var context = new RequestContext();
        var messages = new List<Message>();
        for (var i = 0; i < 10; i++)
        {
            var message = CreateMessage("test_topic");
            messages.Add(message);
            _dynamoDbOutbox.Add(message, context);
        }

        var messageIds = messages.Select(m => m.Id);
        var messagesFromOutbox = _dynamoDbOutbox.Get(messageIds, context);

        foreach (var message in messages)
        {
            var messageFromOutbox = messagesFromOutbox.SingleOrDefault(m => m.Id == message.Id);
            Assert.NotNull(messageFromOutbox);
            Assert.Equal(message.Body.Value, messageFromOutbox.Body.Value);
        }
    }

    [Fact]
    public async Task When_getting_a_batch_of_messages_from_the_outbox_async()
    {
        var context = new RequestContext();
        var messages = new List<Message>();
        for (var i = 0; i < 10; i++)
        {
            var message = CreateMessage("test_topic");
            messages.Add(message);
            await _dynamoDbOutbox.AddAsync(message, context);
        }

        var messageIds = messages.Select(m => m.Id);
        var messagesFromOutbox = await _dynamoDbOutbox.GetAsync(messageIds, context);

        foreach (var message in messages)
        {
            var messageFromOutbox = messagesFromOutbox.SingleOrDefault(m => m.Id == message.Id);
            Assert.NotNull(messageFromOutbox);
            Assert.Equal(message.Body.Value, messageFromOutbox.Body.Value);
        }
    }

    private Message CreateMessage(string topic)
    {
        return new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(topic), MessageType.MT_DOCUMENT),
            new MessageBody("message body")
        );
    }
}
