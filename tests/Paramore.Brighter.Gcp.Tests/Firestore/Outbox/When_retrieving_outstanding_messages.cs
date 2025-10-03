using System;
using System.Linq;
using Paramore.Brighter.Outbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Firestore.Outbox;

[Trait("Category", "Firestore")]
public class FetchOutStandingMessageTests
{
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly FirestoreOutbox _outbox;

    public FetchOutStandingMessageTests()
    {
        _outbox = new(Configuration.CreateOutbox());
        var routingKey = new RoutingKey("test_topic");

        _messageEarliest = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_DOCUMENT)
            {
                TimeStamp = DateTimeOffset.UtcNow.AddHours(-3)
            },
            new MessageBody("message body"));
        _messageDispatched = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageUnDispatched = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_DOCUMENT),
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
       Assert.True(total >= 2);

        allUnDispatched = allUnDispatched.ToList();
        Assert.True(allUnDispatched.Count() > 2);
        Assert.Contains(allUnDispatched, x => x.Id == _messageUnDispatched.Id);
        Assert.Contains(allUnDispatched, x => x.Id == _messageEarliest.Id);
        Assert.DoesNotContain(allUnDispatched, x => x.Id == _messageDispatched.Id);

        messagesOverAnHour = messagesOverAnHour.ToList();
        Assert.True(messagesOverAnHour.Count() > 1);
        Assert.Contains(messagesOverAnHour, x => x.Id == _messageEarliest.Id);
        
        messagesOver4Hours = messagesOver4Hours.ToList();
        Assert.DoesNotContain(messagesOver4Hours, x => x.Id == _messageUnDispatched.Id);
        Assert.DoesNotContain(messagesOver4Hours, x => x.Id == _messageEarliest.Id);
        Assert.DoesNotContain(messagesOver4Hours, x => x.Id == _messageDispatched.Id);
    }
    
    [Fact]
    public void When_Retrieving_Not_Dispatched_Messages_With_TrippedTopics()
    {
        var context = new RequestContext();
        _outbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _outbox.MarkDispatched(_messageDispatched.Id, context);
        
        var noMessages = _outbox.OutstandingMessages(TimeSpan.Zero, context, trippedTopics: [new RoutingKey("test_topic")]);
        var allUnDispatched = _outbox.OutstandingMessages(TimeSpan.Zero, context, trippedTopics: [new RoutingKey("not_exists")]);

        //Assert
        allUnDispatched = allUnDispatched.ToList();
        Assert.Contains(allUnDispatched, x => x.Id == _messageUnDispatched.Id);
        Assert.Contains(allUnDispatched, x => x.Id == _messageEarliest.Id);
        Assert.DoesNotContain(allUnDispatched, x => x.Id == _messageDispatched.Id);
        

        noMessages = noMessages.ToList();
        Assert.DoesNotContain(noMessages, x => x.Id == _messageUnDispatched.Id);
        Assert.DoesNotContain(noMessages, x => x.Id == _messageEarliest.Id);
        Assert.DoesNotContain(noMessages, x => x.Id == _messageDispatched.Id);
    }
}
