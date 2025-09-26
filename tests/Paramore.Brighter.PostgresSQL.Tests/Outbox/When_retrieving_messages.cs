using System;
using System.Linq;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

[Trait("Category", "PostgresSql")]
public class PostgresSqlFetchMessageTests : IDisposable
{
    private readonly PostgresSqlTestHelper _postgresSqlTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly PostgreSqlOutbox _sqlOutbox;

    public PostgresSqlFetchMessageTests()
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
    public void When_Retrieving_Messages()
    {
        var context = new RequestContext();
        _sqlOutbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _sqlOutbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _sqlOutbox.MarkDispatched(_messageDispatched.Id, context);

        var messages = _sqlOutbox.Get(null);

        //Assert
        Assert.Equal(3, messages.Count);
    }

    [Fact]
    public void When_Retrieving_Messages_By_Id()
    {
        var context = new RequestContext();
        _sqlOutbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _sqlOutbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _sqlOutbox.MarkDispatched(_messageDispatched.Id, context);

        var messages = _sqlOutbox.Get(
            [_messageEarliest.Id, _messageUnDispatched.Id],
            context);

        // Assert
        var messageList = messages.ToList();
        Assert.Equal(2, messageList.Count);
        Assert.Contains(messageList, x => x.Id == _messageEarliest.Id);
        Assert.Contains(messageList, x => x.Id == _messageUnDispatched.Id);
        Assert.DoesNotContain(messageList, x => x.Id == _messageDispatched.Id);
    }

    [Fact]
    public void When_Retrieving_Message_By_Id()
    {
        var context = new RequestContext();
        _sqlOutbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _sqlOutbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _sqlOutbox.MarkDispatched(_messageDispatched.Id, context);

        var message = _sqlOutbox.Get(_messageDispatched.Id, context);

        // Assert
        Assert.Equal(_messageDispatched.Id, message.Id);
    }


    public void Dispose()
    {
        _postgresSqlTestHelper.CleanUpDb();
    }
}
