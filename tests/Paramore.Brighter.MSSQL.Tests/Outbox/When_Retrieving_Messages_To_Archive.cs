﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

[Trait("Category", "MSSQL")]
public class MsSqlArchiveFetchTests : IDisposable
{

    private readonly MsSqlTestHelper _msSqlTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MsSqlOutbox _sqlOutbox;

    public MsSqlArchiveFetchTests()
    {
        _msSqlTestHelper = new MsSqlTestHelper();
        _msSqlTestHelper.SetupMessageDb();

        _sqlOutbox = new MsSqlOutbox(_msSqlTestHelper.OutboxConfiguration);
        _messageEarliest = new Message(new MessageHeader(Guid.NewGuid().ToString(), "test_topic", MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageDispatched = new Message(new MessageHeader(Guid.NewGuid().ToString(), "test_topic", MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageUnDispatched = new Message(new MessageHeader(Guid.NewGuid().ToString(), "test_topic", MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
    }
    
    [Fact]
    public async Task When_Retrieving_Messages_To_Archive_Async()
    {
        await _sqlOutbox.AddAsync(new []{_messageEarliest, _messageDispatched, _messageUnDispatched});
        await _sqlOutbox.MarkDispatchedAsync(_messageEarliest.Id, DateTime.UtcNow.AddHours(-3));
        await _sqlOutbox.MarkDispatchedAsync(_messageDispatched.Id);
        
        var allDispatched = 
            await _sqlOutbox.DispatchedMessagesAsync(0, 100, cancellationToken: CancellationToken.None);
        var messagesOverAnHour = 
            await _sqlOutbox.DispatchedMessagesAsync(1, 100, cancellationToken: CancellationToken.None);
        var messagesOver4Hours = 
            await _sqlOutbox.DispatchedMessagesAsync(4, 100, cancellationToken: CancellationToken.None);
        
        //Assert
        Assert.Equal(2, allDispatched.Count());
        Assert.Single(messagesOverAnHour);
        Assert.Empty(messagesOver4Hours);
    }
    
    public void Dispose()
    {
        _msSqlTestHelper.CleanUpDb();
    }
}
