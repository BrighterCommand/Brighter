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
    [Fact]
    public async Task When_Archiving_Old_Messages_From_The_Outbox()
    {
        //arrange
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
        var outbox = new InMemoryOutbox(timeProvider);
        var archiveProvider = new InMemoryArchiveProvider();
        
        IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
            producerRegistry, 
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            outbox,
            archiveProvider 
        );

        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await outbox.AddAsync(messageOne);
        await outbox.MarkDispatchedAsync(messageOne.Id);
        
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await outbox.AddAsync(messageTwo);
        await outbox.MarkDispatchedAsync(messageTwo.Id);
        
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        await outbox.AddAsync(messageThree);
        await outbox.MarkDispatchedAsync(messageThree.Id);

        //act
        outbox.EntryCount.Should().Be(3);
        
        await bus.ArchiveAsync(20000, new CancellationToken());
        
        //assert
        outbox.EntryCount.Should().Be(0);
        archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageOne.Id, messageOne));
        archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageTwo.Id, messageTwo));
        archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageThree.Id, messageThree));
        
    }
}
