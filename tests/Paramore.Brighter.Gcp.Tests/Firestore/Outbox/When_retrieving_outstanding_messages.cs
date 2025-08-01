using System;
using System.Linq;
using Paramore.Brighter.Outbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Firestore.Outbox;

[Trait("Category", "Firestore")]
public class FetchOutStandingMessageTests : IDisposable
{
    private readonly string _collection;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly FirestoreOutbox _outbox;

    public FetchOutStandingMessageTests()
    {
        _collection = $"outbox-{Guid.NewGuid():N}";
        _outbox = new(Configuration.CreateOutbox(_collection));
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
        _outbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _outbox.MarkDispatched(_messageDispatched.Id, context);
        
        var total = _outbox.GetNumberOfOutstandingMessages();

        var allUnDispatched = _outbox.OutstandingMessages(TimeSpan.Zero, context);
        var messagesOverAnHour = _outbox.OutstandingMessages(TimeSpan.FromHours(1), context);
        var messagesOver4Hours = _outbox.OutstandingMessages(TimeSpan.FromHours(4), context);

        //Assert
        Assert.Equal(2, total);
        Assert.Equal(2, allUnDispatched.Count());
        Assert.Single(messagesOverAnHour);
        Assert.Empty(messagesOver4Hours);
    }
    
    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
