using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

[Trait("Category", "Sqlite")]
public class SqliteArchiveFetchTests : IAsyncDisposable
{
    private readonly SqliteTestHelper _sqliteTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly SqliteOutbox _sqlOutbox;

    public SqliteArchiveFetchTests()
    {
        _sqliteTestHelper = new SqliteTestHelper();
        _sqliteTestHelper.SetupMessageDb();
        
        _sqlOutbox = new SqliteOutbox(_sqliteTestHelper.OutboxConfiguration);
        var routingKey = new RoutingKey("test_topic");

        _messageEarliest = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageUnDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
    }

    [Fact]
    public void When_Retrieving_Messages_To_Archive()
    {
        var context = new RequestContext();
        _sqlOutbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _sqlOutbox.MarkDispatched(_messageEarliest.Id, context, DateTimeOffset.UtcNow.AddHours(-3));
        _sqlOutbox.MarkDispatched(_messageDispatched.Id, context);

        var allDispatched = _sqlOutbox.DispatchedMessages(0, context);
        var messagesOverAnHour = _sqlOutbox.DispatchedMessages(1, context);
        var messagesOver4Hours = _sqlOutbox.DispatchedMessages(4, context);

        //Assert
        Assert.Equal(2, allDispatched.Count());
        Assert.Single(messagesOverAnHour);
        Assert.Empty(messagesOver4Hours);
    }

    [Fact]
    public void When_Retrieving_Messages_To_Archive_UsingTimeSpan()
    {
        var context = new RequestContext();
        _sqlOutbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _sqlOutbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _sqlOutbox.MarkDispatched(_messageDispatched.Id, context);

        var allDispatched = _sqlOutbox.DispatchedMessages(TimeSpan.Zero, context);
        var messagesOverAnHour = _sqlOutbox.DispatchedMessages(TimeSpan.FromHours(2), context);
        var messagesOver4Hours = _sqlOutbox.DispatchedMessages(TimeSpan.FromHours(4), context);

        //Assert
        Assert.Equal(2, allDispatched.Count());
        Assert.Single(messagesOverAnHour);
        Assert.Empty(messagesOver4Hours ?? []);
    }

    public async ValueTask DisposeAsync()
    {
        await _sqliteTestHelper.CleanUpDbAsync();
    }
}
