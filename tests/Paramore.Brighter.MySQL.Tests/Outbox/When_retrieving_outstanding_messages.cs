﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

[Trait("Category", "MySql")]
public class MySqlFetchOutStandingMessageTests : IDisposable
{
    private readonly MySqlTestHelper _mySqlTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MySqlOutbox _sqlOutbox;

    public MySqlFetchOutStandingMessageTests()
    {
        _mySqlTestHelper = new MySqlTestHelper();
        _mySqlTestHelper.SetupMessageDb();

        _sqlOutbox = new MySqlOutbox(_mySqlTestHelper.OutboxConfiguration);
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
    public void When_Retrieving_Not_Dispatched_Messages()
    {
        var context = new RequestContext();
        _sqlOutbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _sqlOutbox.MarkDispatched(_messageDispatched.Id, context);
        
        var total = _sqlOutbox.GetNumberOfOutstandingMessages();

        var allUnDispatched = _sqlOutbox.OutstandingMessages(TimeSpan.Zero, context);
        var messagesOverAnHour = _sqlOutbox.OutstandingMessages(TimeSpan.FromHours(1), context);
        var messagesOver4Hours = _sqlOutbox.OutstandingMessages(TimeSpan.FromHours(4), context);

        //Assert
        Assert.Equal(2, total);
        Assert.Equal(2, (allUnDispatched)?.Count());
        Assert.Single(messagesOverAnHour);
        Assert.Empty(messagesOver4Hours ?? []);
    }

    public void Dispose()
    {
        _mySqlTestHelper.CleanUpDb();
    }
}
