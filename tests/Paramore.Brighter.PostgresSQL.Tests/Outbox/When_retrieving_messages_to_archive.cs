using System;
using FluentAssertions;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

[Trait("Category", "PostgresSql")]
public class PostgresSqlArchiveFetchTests : IDisposable
{
    private readonly PostgresSqlTestHelper _postgresSqlTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly PostgreSqlOutbox _sqlOutbox;

    public PostgresSqlArchiveFetchTests()
    {
        _postgresSqlTestHelper = new PostgresSqlTestHelper();
        _postgresSqlTestHelper.SetupMessageDb();

        _sqlOutbox = new PostgreSqlOutbox(_postgresSqlTestHelper.Configuration);
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
        allDispatched.Should().HaveCount(2);
        messagesOverAnHour.Should().ContainSingle();
        messagesOver4Hours.Should().BeEmpty();
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
        allDispatched.Should().HaveCount(2);
        messagesOverAnHour.Should().ContainSingle();
        messagesOver4Hours.Should().BeEmpty();
    }

    public void Dispose()
    {
        _postgresSqlTestHelper.CleanUpDb();
    }
}
