using System;
using System.Linq;
using Paramore.Brighter.Outbox.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Spanner.Outbox;

[Trait("Category", "Spanner")]
public class PostgresSqlFetchOutStandingMessageTests : IDisposable
{
    private readonly SpannerTestHelper _spannerTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly SpannerOutbox _sqlOutbox;

    public PostgresSqlFetchOutStandingMessageTests()
    {
        _spannerTestHelper = new SpannerTestHelper();
        _spannerTestHelper.SetupMessageDb();

        _sqlOutbox = new(_spannerTestHelper.Configuration);
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
        
        var total = _sqlOutbox.GetNumberOfOutstandingMessages(null);

        var allUnDispatched = _sqlOutbox.OutstandingMessages(TimeSpan.Zero, context);
        var messagesOverAnHour = _sqlOutbox.OutstandingMessages(TimeSpan.FromHours(1), context);
        var messagesOver4Hours = _sqlOutbox.OutstandingMessages(TimeSpan.FromHours(4), context);

        //Assert
        Assert.Equal(2, total);
        Assert.Equal(2, allUnDispatched.Count());
        Assert.Single(messagesOverAnHour);
        Assert.Empty(messagesOver4Hours);
    }

    public void Dispose()
    {
        _spannerTestHelper.CleanUpDb();
    }
}
