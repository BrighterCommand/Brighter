using System;
using System.Linq;
using Paramore.Brighter.Outbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Firestore.Outbox;

[Trait("Category", "Firestore")]
public class ArchiveFetchTests
{
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly FirestoreOutbox _outbox;

    public ArchiveFetchTests()
    {
        _outbox = new(Configuration.CreateOutbox());
        var routingKey = new RoutingKey("test_topic");

        _messageEarliest = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageDispatched = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageUnDispatched = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
    }

    [Fact]
    public void When_Retrieving_Messages_To_Archive_UsingTimeSpan()
    {
        var context = new RequestContext();
        _outbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _outbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _outbox.MarkDispatched(_messageDispatched.Id, context);

        var allDispatched = _outbox.DispatchedMessages(TimeSpan.Zero, context);
        var messagesOverAnHour = _outbox.DispatchedMessages(TimeSpan.FromHours(2), context);
        var messagesOver4Hours = _outbox.DispatchedMessages(TimeSpan.FromHours(4), context);

        //Assert
        allDispatched = allDispatched.ToList();
        Assert.True(allDispatched.Count() > 2);
        Assert.Contains(allDispatched, x => x.Id == _messageEarliest.Id);
        Assert.Contains(allDispatched, x => x.Id == _messageDispatched.Id);
        Assert.DoesNotContain(allDispatched, x => x.Id == _messageUnDispatched.Id);
        Assert.Empty(messagesOverAnHour);
        Assert.Empty(messagesOver4Hours);
    }
}
