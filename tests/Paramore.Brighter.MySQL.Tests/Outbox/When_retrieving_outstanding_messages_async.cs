using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

[Trait("Category", "MySql")]
public class MySqlFetchOutStandingMessageAsyncTests : IDisposable
{
    private readonly MySqlTestHelper _mySqlTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MySqlOutbox _sqlOutbox;

    public MySqlFetchOutStandingMessageAsyncTests()
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
        total.Should().Be(2);
        allUnDispatched.Should().HaveCount(2);
        messagesOverAnHour.Should().ContainSingle();
        messagesOver4Hours.Should().BeEmpty();
    }

    public void Dispose()
    {
        _mySqlTestHelper.CleanUpDb();
    }
}
