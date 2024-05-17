using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Archiving;

public class ServiceBusMessageStoreArchiverTestsAsync 
{
    private readonly InMemoryOutbox _outbox;
    private readonly InMemoryArchiveProvider _archiveProvider;
    private readonly ExternalBusService<Message,CommittableTransaction> _bus;

    public ServiceBusMessageStoreArchiverTestsAsync()
    {
        const string topic = "MyTopic";

        var producer = new FakeMessageProducerWithPublishConfirmation{Publication = {Topic = new RoutingKey(topic), RequestType = typeof(MyCommand)}};

        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync())
        );

        var retryPolicy = Policy
            .Handle<Exception>()
            .RetryAsync();

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

        var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
        {
            { topic, producer },
        });

        var policyRegistry = new PolicyRegistry
        {
            { CommandProcessor.RETRYPOLICYASYNC, retryPolicy },
            { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy }
        }; 
        
        var timeProvider = new FakeTimeProvider();
        _outbox = new InMemoryOutbox(timeProvider);
        _archiveProvider = new InMemoryArchiveProvider();
        
        _bus = new ExternalBusService<Message, CommittableTransaction>(
            producerRegistry, 
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            _outbox,
            _archiveProvider 
        );
 
    }
    
    [Fact]
    public async Task When_Archiving_Old_Messages_From_The_Outbox()
    {
        //arrange
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageOne);
        await _outbox.MarkDispatchedAsync(messageOne.Id);
        
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageTwo);
        await _outbox.MarkDispatchedAsync(messageTwo.Id);
        
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageThree);
        await _outbox.MarkDispatchedAsync(messageThree.Id);

        //act
        _outbox.EntryCount.Should().Be(3);
        
        await _bus.ArchiveAsync(20000, new CancellationToken());
        
        //assert
        _outbox.EntryCount.Should().Be(0);
        _archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageOne.Id, messageOne));
        _archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageTwo.Id, messageTwo));
        _archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageThree.Id, messageThree));
    }
    
    [Fact]
    public async Task When_Archiving_Some_Messages_From_The_Outbox()
    {
        //arrange
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageOne);
        await _outbox.MarkDispatchedAsync(messageOne.Id);
        
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageTwo);
        await _outbox.MarkDispatchedAsync(messageTwo.Id);
        
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageThree);

        //act
        _outbox.EntryCount.Should().Be(3);
        
        await _bus.ArchiveAsync(20000, new CancellationToken());
        
        //assert
        _outbox.EntryCount.Should().Be(1);
        _archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageOne.Id, messageOne));
        _archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageTwo.Id, messageTwo));
        _archiveProvider.ArchivedMessages.Should().NotContain(new KeyValuePair<string, Message>(messageThree.Id, messageThree));
    }
    
    [Fact]
    public async Task When_Archiving_No_Messages_From_The_Outbox()
    {
        //arrange
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageOne);
        
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageTwo);
        
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await _outbox.AddAsync(messageThree);

        //act
        _outbox.EntryCount.Should().Be(3);
        
        await _bus.ArchiveAsync(20000, new CancellationToken());
        
        //assert
        _outbox.EntryCount.Should().Be(3);
        _archiveProvider.ArchivedMessages.Should().NotContain(new KeyValuePair<string, Message>(messageOne.Id, messageOne));
        _archiveProvider.ArchivedMessages.Should().NotContain(new KeyValuePair<string, Message>(messageTwo.Id, messageTwo));
        _archiveProvider.ArchivedMessages.Should().NotContain(new KeyValuePair<string, Message>(messageThree.Id, messageThree));
    }
    
    [Fact]
    public async Task When_Archiving_An_Empty_Outbox()
    {
        //act
        await _bus.ArchiveAsync(20000, new CancellationToken());
        
        //assert
        _outbox.EntryCount.Should().Be(0);
    }
}
