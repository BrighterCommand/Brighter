using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Archiving;
public class ServiceBusMessageStoreArchiverTestsAsync
{
    private readonly InMemoryOutbox _outbox;
    private readonly InMemoryArchiveProvider _archiveProvider;
    private readonly FakeTimeProvider _timeProvider;
    private readonly RoutingKey _routingKey = new("MyTopic");
    private readonly OutboxArchiver<Message, CommittableTransaction> _archiver;
    public ServiceBusMessageStoreArchiverTestsAsync()
    {
        _timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer();
        _outbox = new InMemoryOutbox(_timeProvider)
        {
            Tracer = tracer
        };
        _archiveProvider = new InMemoryArchiveProvider();
        _archiver = new OutboxArchiver<Message, CommittableTransaction>(_outbox, _archiveProvider);
    }

    [Test]
    public async Task When_Archiving_Old_Messages_From_The_Outbox()
    {
        //arrange
        var context = new RequestContext();
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageOne, context);
        await _outbox.MarkDispatchedAsync(messageOne.Id, context);
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageTwo, context);
        await _outbox.MarkDispatchedAsync(messageTwo.Id, context);
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageThree, context);
        await _outbox.MarkDispatchedAsync(messageThree.Id, context);
        //act
        await Assert.That(_outbox.EntryCount).IsEqualTo(3);
        _timeProvider.Advance(TimeSpan.FromSeconds(30));
        await _archiver.ArchiveAsync(TimeSpan.FromSeconds(15), context);
        //assert
        await Assert.That(_outbox.EntryCount).IsEqualTo(0);
        await Assert.That(_archiveProvider.ArchivedMessages).Contains(new KeyValuePair<string, Message>(messageOne.Id, messageOne));
        await Assert.That(_archiveProvider.ArchivedMessages).Contains(new KeyValuePair<string, Message>(messageTwo.Id, messageTwo));
        await Assert.That(_archiveProvider.ArchivedMessages).Contains(new KeyValuePair<string, Message>(messageThree.Id, messageThree));
    }

    [Test]
    public async Task When_Archiving_Some_Messages_From_The_Outbox()
    {
        //arrange
        var context = new RequestContext();
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageOne, context);
        await _outbox.MarkDispatchedAsync(messageOne.Id, context);
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageTwo, context);
        await _outbox.MarkDispatchedAsync(messageTwo.Id, context);
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageThree, context);
        //act
        await Assert.That(_outbox.EntryCount).IsEqualTo(3);
        _timeProvider.Advance(TimeSpan.FromSeconds(30));
        await _archiver.ArchiveAsync(TimeSpan.FromSeconds(15), context);
        //assert
        await Assert.That(_outbox.EntryCount).IsEqualTo(1);
        await Assert.That(_archiveProvider.ArchivedMessages).Contains(new KeyValuePair<string, Message>(messageOne.Id, messageOne));
        await Assert.That(_archiveProvider.ArchivedMessages).Contains(new KeyValuePair<string, Message>(messageTwo.Id, messageTwo));
        await Assert.That(_archiveProvider.ArchivedMessages).DoesNotContain(new KeyValuePair<string, Message>(messageThree.Id, messageThree));
    }

    [Test]
    public async Task When_Archiving_No_Messages_From_The_Outbox()
    {
        //arrange
        var context = new RequestContext();
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageOne, context);
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageTwo, context);
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageThree, context);
        //act
        await Assert.That(_outbox.EntryCount).IsEqualTo(3);
        await _archiver.ArchiveAsync(TimeSpan.FromMilliseconds(20000), context);
        //assert
        await Assert.That(_outbox.EntryCount).IsEqualTo(3);
        await Assert.That(_archiveProvider.ArchivedMessages).DoesNotContain(new KeyValuePair<string, Message>(messageOne.Id, messageOne));
        await Assert.That(_archiveProvider.ArchivedMessages).DoesNotContain(new KeyValuePair<string, Message>(messageTwo.Id, messageTwo));
        await Assert.That(_archiveProvider.ArchivedMessages).DoesNotContain(new KeyValuePair<string, Message>(messageThree.Id, messageThree));
    }

    [Test]
    public async Task When_Archiving_An_Empty_Outbox()
    {
        //arrange
        var context = new RequestContext();
        //act
        await _archiver.ArchiveAsync(TimeSpan.FromMilliseconds(20000), context);
        //assert
        await Assert.That(_outbox.EntryCount).IsEqualTo(0);
    }
}