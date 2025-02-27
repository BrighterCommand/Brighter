using System;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

[Trait("Category", "Sqlite")]
public class PostgresSqlFetchOutStandingMessageAsyncTests : IAsyncDisposable
{
    private readonly SqliteTestHelper _sqliteTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly SqliteOutbox _sqlOutbox;

    public PostgresSqlFetchOutStandingMessageAsyncTests()
    {
        _sqliteTestHelper = new SqliteTestHelper();
        _sqliteTestHelper.SetupMessageDb();

        _sqlOutbox = new SqliteOutbox(_sqliteTestHelper.OutboxConfiguration);
        var routingKey = new RoutingKey("test_topic");

        _messageEarliest = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT)
            {
                TimeStamp = DateTimeOffset.UtcNow.AddHours(-3)
            },
            new MessageBody("message body"));
        _messageDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageUnDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
    }

    [Fact]
    public async Task When_Retrieving_Not_Dispatched_Messages_Async()
    {
        var context = new RequestContext();
        await _sqlOutbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _sqlOutbox.MarkDispatchedAsync(_messageDispatched.Id, context);
        
        var total = await _sqlOutbox.GetNumberOfOutstandingMessagesAsync();

        var allUnDispatched = await _sqlOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context);
        var messagesOverAnHour = await _sqlOutbox.OutstandingMessagesAsync(TimeSpan.FromHours(1), context);
        var messagesOver4Hours = await _sqlOutbox.OutstandingMessagesAsync(TimeSpan.FromHours(4), context);

        //Assert
        Assert.Equal(2, total);
        Assert.Equal(2, (allUnDispatched)?.Count());
        messagesOverAnHour.Should().ContainSingle();
        Assert.Empty(messagesOver4Hours ?? []);
    }

    public async ValueTask DisposeAsync()
    {
        await _sqliteTestHelper.CleanUpDbAsync();
    }
}
