using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Trait("Category", "MongoDb")]
public class MongoDbFetchOutStandingMessageAsyncTests : IDisposable
{
    private readonly string _collection;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MongoDbOutbox _outbox;

    public MongoDbFetchOutStandingMessageAsyncTests()
    {
        _collection = $"outbox-{Guid.NewGuid():N}";
        _outbox = new MongoDbOutbox(Configuration.CreateOutbox(_collection));
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
        await _outbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _outbox.MarkDispatchedAsync(_messageDispatched.Id, context);
        
        var total = await _outbox.GetNumberOfOutstandingMessagesAsync();

        var allUnDispatched = await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context);
        var messagesOverAnHour = await _outbox.OutstandingMessagesAsync(TimeSpan.FromHours(1), context);
        var messagesOver4Hours = await _outbox.OutstandingMessagesAsync(TimeSpan.FromHours(4), context);

        //Assert
        Assert.Equal(2, total);
        Assert.Equal(2, allUnDispatched.Count());
        Assert.Single(messagesOverAnHour);
        Assert.Empty(messagesOver4Hours);
    }
    
    [Fact]
    public async Task When_Retrieving_Not_Dispatched_Messages_With_TrippedTopic_Async()
    {
        var context = new RequestContext();
        await _outbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _outbox.MarkDispatchedAsync(_messageDispatched.Id, context);
        
        var allUnDispatched = await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context, trippedTopics: [new RoutingKey("test_topic")]);

        //Assert
        Assert.Empty(allUnDispatched);
        
        allUnDispatched = await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context, trippedTopics: [new RoutingKey("other_topic")]);
        Assert.Equal(2, allUnDispatched.Count());
    }

    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
