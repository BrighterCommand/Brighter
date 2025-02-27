using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

[Trait("Category", "MySql")]
public class MySqlArchiveFetchTests : IDisposable
{
    private readonly MySqlTestHelper _mySqlTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MySqlOutbox _sqlOutbox;

    public MySqlArchiveFetchTests()
    {
        _mySqlTestHelper = new MySqlTestHelper();
        _mySqlTestHelper.SetupMessageDb();

        _sqlOutbox = new MySqlOutbox(_mySqlTestHelper.OutboxConfiguration);
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
        Assert.Equal(2, (allDispatched)?.Count());
        messagesOverAnHour.Should().ContainSingle();
        Assert.Empty(messagesOver4Hours ?? []);
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
        Assert.Equal(2, (allDispatched)?.Count());
        messagesOverAnHour.Should().ContainSingle();
        Assert.Empty(messagesOver4Hours ?? []);
    }

    public void Dispose()
    {
        _mySqlTestHelper.CleanUpDb();
    }
}
