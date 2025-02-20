using System;
using FluentAssertions;
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
        _outbox = new MongoDbOutbox(Configuration.Create(_collection));
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
        total.Should().Be(2);
        allUnDispatched.Should().HaveCount(2);
        messagesOverAnHour.Should().ContainSingle();
        messagesOver4Hours.Should().BeEmpty();
    }
    
    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
