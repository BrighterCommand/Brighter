using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Firestore.Outbox;

[Trait("Category", "Firestore")]
public class FetchMessageAsyncTests
{
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly FirestoreOutbox _outbox;

    public FetchMessageAsyncTests()
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
    public async Task When_Retrieving_Messages_Async()
    {
        var context = new RequestContext();
        await _outbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _outbox.MarkDispatchedAsync(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await _outbox.MarkDispatchedAsync(_messageDispatched.Id, context);
        
        var messages = await _outbox.GetAsync(pageSize: 1_000);

        //Assert
        Assert.True(messages.Count() >= 3);
        Assert.Contains(messages, message => message.Id == _messageEarliest.Id);
        Assert.Contains(messages, message => message.Id == _messageDispatched.Id);
        Assert.Contains(messages, message => message.Id == _messageUnDispatched.Id);
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
        //Assert
        Assert.NotNull(messages);
        Assert.Equal(2, messages.Count());
        Assert.Contains(messages, message => message.Id == _messageEarliest.Id);
        Assert.Contains(messages, message => message.Id == _messageUnDispatched.Id);
        Assert.Contains(messages, message => message.Id == _messageUnDispatched.Id);
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
}
