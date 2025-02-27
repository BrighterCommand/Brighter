using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

[Trait("Category", "MySql")]
public class MySqlFetchMessageTests : IDisposable
{
    private readonly MySqlTestHelper _mySqlTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MySqlOutbox _sqlOutbox;

    public MySqlFetchMessageTests()
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
    public void When_Retrieving_Messages()
    {
        var context = new RequestContext();
        _sqlOutbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _sqlOutbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _sqlOutbox.MarkDispatched(_messageDispatched.Id, context);

        var messages = _sqlOutbox.Get();

        //Assert
        Assert.Equal(3, (messages)?.Count());
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

        //Assert
        messages = messages.ToList();
        Assert.Equal(2, (messages)?.Count());
        Assert.Contains(x => x.Id == _messageEarliest.Id, messages);
        Assert.Contains(x => x.Id == _messageUnDispatched.Id, messages);
        Assert.DoesNotContain(x => x.Id == _messageDispatched.Id, messages);
    }

    [Fact]
    public void When_Retrieving_Message_By_Id()
    {
        var context = new RequestContext();
        _sqlOutbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _sqlOutbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _sqlOutbox.MarkDispatched(_messageDispatched.Id, context);

        var messages = _sqlOutbox.Get(_messageDispatched.Id, context);

        //Assert
        Assert.Equal(_messageDispatched.Id, messages.Id);
    }


    public void Dispose()
    {
        _mySqlTestHelper.CleanUpDb();
    }
}
