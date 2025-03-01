using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Trait("Category", "MongoDb")]
public class MongoDbFetchMessageAsyncTests : IDisposable
{
    private readonly string _collection;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MongoDbOutbox _outbox;

    public MongoDbFetchMessageAsyncTests()
    {
        _collection = $"outbox-{Guid.NewGuid():N}";
        _outbox = new MongoDbOutbox(Configuration.Create(_collection));
        var routingKey = new RoutingKey("test_topic");

        _messageEarliest = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageUnDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
    }

    [Fact]
    public async Task When_Retrieving_Messages_Async()
    {
        var context = new RequestContext();
        await _outbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _outbox.MarkDispatchedAsync(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await _outbox.MarkDispatchedAsync(_messageDispatched.Id, context);

        var messages = await _outbox.GetAsync();

        //Assert
        Assert.Equal(3, (messages)?.Count());
    }

    [Fact]
    public async Task When_Retrieving_Messages_By_Id_Async()
    {
        var context = new RequestContext();
        await _outbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _outbox.MarkDispatchedAsync(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await _outbox.MarkDispatchedAsync(_messageDispatched.Id, context);

        var messages = await _outbox.GetAsync(
            [_messageEarliest.Id, _messageUnDispatched.Id],
            context);

        //Assert
        messages = messages.ToList();
        Assert.Equal(2, (messages)?.Count());
        Assert.Contains(x => x.Id == _messageEarliest.Id, messages);
        Assert.Contains(x => x.Id == _messageUnDispatched.Id, messages);
        Assert.DoesNotContain(x => x.Id == _messageDispatched.Id, messages);
    }

    [Fact]
    public async Task When_Retrieving_Message_By_Id_Async()
    {
        var context = new RequestContext();
        await _outbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _outbox.MarkDispatchedAsync(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await _outbox.MarkDispatchedAsync(_messageDispatched.Id, context);

        var messages = await _outbox.GetAsync(_messageDispatched.Id, context);

        //Assert
        Assert.Equal(_messageDispatched.Id, messages.Id);
    }

    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
