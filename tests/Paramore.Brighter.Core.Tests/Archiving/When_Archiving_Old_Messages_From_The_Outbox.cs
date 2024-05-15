using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Archiving;

public class ServiceBusMessageStoreArchiverTests 
{
    [Fact]
    public void When_Archiving_Old_Messages_From_The_Outbox()
    {
        //arrange
        const string topic = "MyTopic";

        var producer = new FakeMessageProducerWithPublishConfirmation{Publication = {Topic = new RoutingKey(topic), RequestType = typeof(MyCommand)}};

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
            null);

        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

        var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
        {
            { topic, producer },
        });

        var policyRegistry = new PolicyRegistry
        {
            { CommandProcessor.RETRYPOLICY, retryPolicy },
            { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
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
        outbox.Add(messageOne);
        outbox.MarkDispatched(messageOne.Id);
        
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        outbox.Add(messageTwo);
        outbox.MarkDispatched(messageTwo.Id);
        
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), new MessageBody("test content"));
        outbox.Add(messageThree);
        outbox.MarkDispatched(messageThree.Id);

        //act
        outbox.EntryCount.Should().Be(3);
        
        bus.Archive(20000);
        
        //assert
        outbox.EntryCount.Should().Be(0);
        archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageOne.Id, messageOne));
        archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageTwo.Id, messageTwo));
        archiveProvider.ArchivedMessages.Should().Contain(new KeyValuePair<string, Message>(messageThree.Id, messageThree));
        
    }
}
