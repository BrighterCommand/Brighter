using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Trait("Category", "MongoDb")]
public class MongoDbArchiveFetchAsyncTests : IDisposable
{
    private readonly string _collection;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MongoDbOutbox _outbox;

    public MongoDbArchiveFetchAsyncTests()
    {
        _collection = $"outbox-{Guid.NewGuid():N}";
        _outbox = new MongoDbOutbox(Configuration.CreateOutbox(_collection));
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
    public async Task When_Retrieving_Messages_To_Archive_UsingTimeSpan_Async()
    {
        var context = new RequestContext();
        await _outbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _outbox.MarkDispatchedAsync(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await _outbox.MarkDispatchedAsync(_messageDispatched.Id, context);

        var allDispatched =
            await _outbox.DispatchedMessagesAsync(TimeSpan.Zero, context,
                cancellationToken: CancellationToken.None);
        var messagesOverAnHour =
            await _outbox.DispatchedMessagesAsync(TimeSpan.FromHours(2), context,
                cancellationToken: CancellationToken.None);
        var messagesOver4Hours =
            await _outbox.DispatchedMessagesAsync(TimeSpan.FromHours(4), context,
                cancellationToken: CancellationToken.None);

        //Assert
        Assert.Equal(2, allDispatched.Count());
        Assert.Single(messagesOverAnHour);
        Assert.Empty(messagesOver4Hours);
    }
    
    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
