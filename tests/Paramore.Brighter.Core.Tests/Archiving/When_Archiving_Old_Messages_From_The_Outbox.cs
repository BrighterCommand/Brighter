using System;
using System.Collections.Generic;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Archiving;

public class ServiceBusMessageStoreArchiverTests 
{
    private readonly InMemoryOutbox _outbox;
    private readonly InMemoryArchiveProvider _archiveProvider;
    private readonly FakeTimeProvider _timeProvider;
    private readonly RoutingKey _routingKey = new("MyTopic");
    private readonly OutboxArchiver<Message,CommittableTransaction> _archiver;

    public ServiceBusMessageStoreArchiverTests()
    {
        _timeProvider = new FakeTimeProvider();

        var tracer = new BrighterTracer();
        _outbox = new InMemoryOutbox(_timeProvider){Tracer = tracer};
        _archiveProvider = new InMemoryArchiveProvider();

        _archiver = new OutboxArchiver<Message, CommittableTransaction>(
            _outbox,
            _archiveProvider
        );

    }
    
    [Fact]
    public void When_Archiving_All_Messages_From_The_Outbox()
    {
        //arrange
        var context = new RequestContext();
        
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        _outbox.Add(messageOne, context);
        _outbox.MarkDispatched(messageOne.Id, context);
        
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        _outbox.Add(messageTwo, context);
        _outbox.MarkDispatched(messageTwo.Id, context);
        
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        _outbox.Add(messageThree, context);
        _outbox.MarkDispatched(messageThree.Id, context);

        //act
        _outbox.EntryCount.Should().Be(3);
        
       _timeProvider.Advance(TimeSpan.FromMinutes(15)); 
       
        _archiver.Archive(TimeSpan.FromMilliseconds(500), context);
        
        //assert
        _outbox.EntryCount.Should().Be(0);
        _archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageOne.Id, messageOne));
        _archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageTwo.Id, messageTwo));
        _archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageThree.Id, messageThree));
    }
    
    [Fact]
    public void When_Archiving_Some_Messages_From_The_Outbox()
    {
        //arrange
        var context = new RequestContext();
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        _outbox.Add(messageOne, context);
        _outbox.MarkDispatched(messageOne.Id, context);
        
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        _outbox.Add(messageTwo, context);
        _outbox.MarkDispatched(messageTwo.Id, context);
        
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        _outbox.Add(messageThree, context);

        //act
        _outbox.EntryCount.Should().Be(3);
        
        _timeProvider.Advance(TimeSpan.FromSeconds(30));
        
        _archiver.Archive(TimeSpan.FromSeconds(30), context);
        
        //assert
        _outbox.EntryCount.Should().Be(1);
        _archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageOne.Id, messageOne));
        _archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageTwo.Id, messageTwo));
        _archiveProvider.ArchivedMessages.Should().NotContain((new KeyValuePair<string, Message>(messageThree.Id, messageThree)));
        
    }
    
    [Fact]
    public void When_Archiving_No_Messages_From_The_Outbox()
    {
        var context = new RequestContext();
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        _outbox.Add(messageOne, context);
        
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        _outbox.Add(messageTwo, context);
        
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        _outbox.Add(messageThree, context);

        //act
        _outbox.EntryCount.Should().Be(3);
        
        _archiver.Archive(TimeSpan.FromMilliseconds(20000), context);
        
        //assert
        _outbox.EntryCount.Should().Be(3);
        _archiveProvider.ArchivedMessages.Should().NotContain(new KeyValuePair<string, Message>(messageOne.Id, messageOne));
        _archiveProvider.ArchivedMessages.Should().NotContain(new KeyValuePair<string, Message>(messageTwo.Id, messageTwo));
        _archiveProvider.ArchivedMessages.Should().NotContain((new KeyValuePair<string, Message>(messageThree.Id, messageThree)));
    }
    
    [Fact]
    public void When_Archiving_An_Empty_The_Outbox()
    {
        var context = new RequestContext();
        _archiver.Archive(TimeSpan.FromMilliseconds(20000), context);
        
        //assert
        _outbox.EntryCount.Should().Be(0);
    }
}
