using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

[Trait("Category", "MySql")]
public class MySqlFetchMessageAsyncTests : IDisposable
{
    private readonly MySqlTestHelper _mySqlTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MySqlOutbox _sqlOutbox;

    public MySqlFetchMessageAsyncTests()
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
    public async Task When_Retrieving_Messages_Async()
    {
        var context = new RequestContext();
        await _sqlOutbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _sqlOutbox.MarkDispatchedAsync(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await _sqlOutbox.MarkDispatchedAsync(_messageDispatched.Id, context);

        var messages = await _sqlOutbox.GetAsync(null);

        //Assert
        Assert.Equal(3, messages.Count);
    }

    [Fact]
    public async Task When_Retrieving_Messages_By_Id_Async()
    {
        var context = new RequestContext();
        await _sqlOutbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _sqlOutbox.MarkDispatchedAsync(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await _sqlOutbox.MarkDispatchedAsync(_messageDispatched.Id, context);

        var messages = await _sqlOutbox.GetAsync(
            [_messageEarliest.Id, _messageUnDispatched.Id],
            context);

        //Assert
        var messageList = messages.ToList();
        Assert.Equal(2, messageList.Count);
        Assert.Contains(messageList, x => x.Id == _messageEarliest.Id);
        Assert.Contains(messageList, x => x.Id == _messageUnDispatched.Id);
        Assert.DoesNotContain(messageList, x => x.Id == _messageDispatched.Id);
    }

    [Fact]
    public async Task When_Retrieving_Message_By_Id_Async()
    {
        var context = new RequestContext();
        await _sqlOutbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _sqlOutbox.MarkDispatchedAsync(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await _sqlOutbox.MarkDispatchedAsync(_messageDispatched.Id, context);

        var message = await _sqlOutbox.GetAsync(_messageDispatched.Id, context);

        //Assert
        Assert.Equal(_messageDispatched.Id, message.Id);
    }

    public void Dispose()
    {
        _mySqlTestHelper.CleanUpDb();
    }
}
