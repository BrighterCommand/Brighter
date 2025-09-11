using System;
using System.Linq;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Trait("Category", "MongoDb")]
public class MongoDbFetchOutStandingMessageTests : IDisposable
{
    private readonly string _collection;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MongoDbOutbox _outbox;

    public MongoDbFetchOutStandingMessageTests()
    {
        _collection = $"outbox-{Guid.NewGuid():N}";
        _outbox = new MongoDbOutbox(Configuration.CreateOutbox(_collection));
        var routingKey = new RoutingKey("test_topic");

        _messageEarliest = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT)
            {
                DataSchema = new Uri("data-schema", UriKind.Relative),
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
    
    [Fact]
    public void When_Retrieving_Not_Dispatched_Messages_With_TrippedTopic()
    {
        var context = new RequestContext();
        _outbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _outbox.MarkDispatched(_messageDispatched.Id, context);
        
        var allUnDispatched = _outbox.OutstandingMessages(TimeSpan.Zero, context, trippedTopics: [new RoutingKey("test_topic")]);

        //Assert
        Assert.Empty(allUnDispatched);
        
        allUnDispatched = _outbox.OutstandingMessages(TimeSpan.Zero, context, trippedTopics: [new RoutingKey("other_topic")]);
        Assert.Equal(2, allUnDispatched.Count());
    }
    
    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
