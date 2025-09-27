using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Outbox.DynamoDB.V4;
using Xunit;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Outbox;

[Trait("Category", "DynamoDB")]
public class DynamoDbOutboxOutstandingMessageCountTests : DynamoDBOutboxBaseTest
{
    private readonly DynamoDbOutbox _dynamoDbOutbox;
    private readonly FakeTimeProvider _fakeTimeProvider;

    public DynamoDbOutboxOutstandingMessageCountTests()
    {
        // Set the fake time provider to an early time to prevent conflicts from other tests
        _fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _dynamoDbOutbox = new DynamoDbOutbox(Client, new DynamoDbConfiguration(OutboxTableName), _fakeTimeProvider);
    }

    [Fact]
    public async Task When_counting_outstanding_messages()
    {
        var context = new RequestContext();
        var initialOutstandingCount = _dynamoDbOutbox.GetOutstandingMessageCount(TimeSpan.Zero, context);

        var messages = new List<Message>();
        for (var i = 0; i < 10; i++)
        {
            var message = CreateMessage("test_topic");
            messages.Add(message);
            _dynamoDbOutbox.Add(message, context);
        }

        // Mark a couple as dispatched
        _dynamoDbOutbox.MarkDispatched(messages[0].Id, context);
        _dynamoDbOutbox.MarkDispatched(messages[1].Id, context);

        // Give the GSI a second to catch up
        await Task.Delay(1000);
        _fakeTimeProvider.Advance(TimeSpan.FromMinutes(1));

        var finalOutstandingCount = _dynamoDbOutbox.GetOutstandingMessageCount(TimeSpan.Zero, context);
        Assert.Equal(initialOutstandingCount + 8, finalOutstandingCount);
    }

    [Fact]
    public async Task When_counting_outstanding_messages_async()
    {
        var context = new RequestContext();
        var initialOutstandingCount = await _dynamoDbOutbox.GetOutstandingMessageCountAsync(TimeSpan.Zero, context);

        var messages = new List<Message>();
        for (var i = 0; i < 10; i++)
        {
            var message = CreateMessage("test_topic");
            messages.Add(message);
            await _dynamoDbOutbox.AddAsync(message, context);
        }

        // Mark a couple as dispatched
        await _dynamoDbOutbox.MarkDispatchedAsync(messages[0].Id, context);
        await _dynamoDbOutbox.MarkDispatchedAsync(messages[1].Id, context);

        // Give the GSI a second to catch up
        await Task.Delay(1000);
        _fakeTimeProvider.Advance(TimeSpan.FromMinutes(1));

        var finalOutstandingCount = await _dynamoDbOutbox.GetOutstandingMessageCountAsync(TimeSpan.Zero, context);
        Assert.Equal(initialOutstandingCount + 8, finalOutstandingCount);
    }

    private Message CreateMessage(string topic)
    {
        return new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(topic), MessageType.MT_DOCUMENT, timeStamp: _fakeTimeProvider.GetUtcNow()),
            new MessageBody("message body")
        );
    }
}
