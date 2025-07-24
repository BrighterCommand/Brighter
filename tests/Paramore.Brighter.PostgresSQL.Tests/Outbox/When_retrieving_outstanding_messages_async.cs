using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

[Trait("Category", "PostgresSql")]
public class PostgresSqlFetchOutStandingMessageAsyncTests : IDisposable
{
    private readonly PostgresSqlTestHelper _postgresSqlTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly Message _messageUnDispatchedWithTrippedTopic;
    private readonly PostgreSqlOutbox  _sqlOutbox;

    public PostgresSqlFetchOutStandingMessageAsyncTests()
    {
        _postgresSqlTestHelper = new PostgresSqlTestHelper();
        _postgresSqlTestHelper.SetupMessageDb();

        _sqlOutbox = new PostgreSqlOutbox(_postgresSqlTestHelper.Configuration);
        var routingKey = new RoutingKey("test_topic");
        var trippedTopicRoutingKey = new RoutingKey("tripped_topic");

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
        _messageUnDispatchedWithTrippedTopic = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), trippedTopicRoutingKey, MessageType.MT_DOCUMENT),
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

    [Fact]
    public void When_Retrieving_Not_Dispatched_Messages_With_Tripped_Topics()
    {
        var circuitBreaker = new InMemoryOutboxCircuitBreaker();
        circuitBreaker.TripTopic(_messageUnDispatchedWithTrippedTopic.Header.Topic.Value);
        var context = new RequestContext();
        _sqlOutbox.Add([_messageUnDispatched, _messageUnDispatchedWithTrippedTopic], context);

        var total = _sqlOutbox.GetNumberOfOutstandingMessages(null);

        var allUnDispatched = _sqlOutbox.OutstandingMessages(TimeSpan.Zero, context);
        var unDispatchedMessageFromOutbox = _sqlOutbox.OutstandingMessages(TimeSpan.Zero, context, trippedTopics: circuitBreaker.TrippedTopics).Single();

        //Assert
        Assert.Equal(2, total);
        Assert.Equal(2, allUnDispatched.Count());
        Assert.Equal(unDispatchedMessageFromOutbox.Id, _messageUnDispatched.Id);
        Assert.Equal(unDispatchedMessageFromOutbox.Header.Topic.Value, _messageUnDispatched.Header.Topic.Value);
    }

    public void Dispose()
    {
        _postgresSqlTestHelper.CleanUpDb();
    }
}
