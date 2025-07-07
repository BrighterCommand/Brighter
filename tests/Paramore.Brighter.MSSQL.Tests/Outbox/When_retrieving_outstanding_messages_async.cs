using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

[Trait("Category", "MSSQL")]
public class MsSqlFetchOutStandingMessageAsyncTests : IDisposable
{
    private readonly MsSqlTestHelper _msSqlTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MsSqlOutbox _sqlOutbox;

    public MsSqlFetchOutStandingMessageAsyncTests()
    {
        _msSqlTestHelper = new MsSqlTestHelper();
        _msSqlTestHelper.SetupMessageDb();

        _sqlOutbox = new MsSqlOutbox(_msSqlTestHelper.OutboxConfiguration);
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
        
        var total = await _sqlOutbox.GetNumberOfOutstandingMessagesAsync(null);

        var allUnDispatched = await _sqlOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context);
        var messagesOverAnHour = await _sqlOutbox.OutstandingMessagesAsync(TimeSpan.FromHours(1), context);
        var messagesOver4Hours = await _sqlOutbox.OutstandingMessagesAsync(TimeSpan.FromHours(4), context);

        //Assert
        Assert.Equal(2, total);
        Assert.Equal(2, allUnDispatched.Count());
        Assert.Single(messagesOverAnHour);
        Assert.Empty(messagesOver4Hours);
    }

    public void Dispose()
    {
        _msSqlTestHelper.CleanUpDb();
    }
}
