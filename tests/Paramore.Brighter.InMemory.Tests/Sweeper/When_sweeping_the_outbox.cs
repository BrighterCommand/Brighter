using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Sweeper
{
    [Trait("Category", "InMemory")]
    public class SweeperTests
    {
        private const string MyTopic = "MyTopic";

        [Fact]
        public async Task When_outstanding_in_outbox_sweep_clears_them()
        {
            //Arrange
            var timeSinceSent = TimeSpan.FromMilliseconds(500);


            var timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            var internalBus = new InternalBus();
            var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { MyTopic, new InMemoryProducer(internalBus, timeProvider) }
            });

            var bus = new ExternalBusService<Message, Transaction>(
                producerRegistry,
                new PolicyRegistry(),
                new MessageMapperRegistry(
                    new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                    new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())
                ),
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                outbox
            ); 

            var commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                bus);

            var sweeper = new OutboxSweeper(timeSinceSent, commandProcessor, new InMemoryRequestContextFactory());

            var messages = new Message[]
            {
                new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()
            };

            foreach (var message in messages)
            {
                commandProcessor.Post(message.ToStubRequest());
            }

            //Act
            timeProvider.Advance(timeSinceSent); // -- let the messages expire

            sweeper.Sweep();

            await Task.Delay(200); //Give the sweep time to run

            //Assert
            internalBus.Stream(new RoutingKey(MyTopic)).Count().Should().Be(3);
            outbox.EntryCount.Should().Be(0);
        }

        [Fact]
        public async Task When_outstanding_in_outbox_sweep_clears_them_async()
        {
            //Arrange
            var timeSinceSent = TimeSpan.FromMilliseconds(500);

            var timeProvider = new FakeTimeProvider();
            
            var tracer = new BrighterTracer(timeProvider);
            var internalBus = new InternalBus();
            var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { MyTopic, new InMemoryProducer(internalBus, timeProvider) }
            });

            var bus = new ExternalBusService<Message, Transaction>(
                producerRegistry,
                new PolicyRegistry(),
                new MessageMapperRegistry(
                    new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                    new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())
                ),
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                outbox
            ); 

            var commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                bus);
            
            var sweeper = new OutboxSweeper(timeSinceSent, commandProcessor, new InMemoryRequestContextFactory());

            var messages = new Message[]
            {
                new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()
            };

            foreach (var message in messages)
            {
                commandProcessor.Post(message.ToStubRequest());
            }

            //Act
            timeProvider.Advance(timeSinceSent); // -- let the messages expire

            sweeper.SweepAsyncOutbox();

            await Task.Delay(200); //Give the sweep time to run

            //Assert
            internalBus.Stream(new RoutingKey(MyTopic)).Count().Should().Be(3);
            outbox.OutstandingMessages(TimeSpan.Zero, new RequestContext()).Count().Should().Be(0);
        }

        [Fact]
        public async Task When_too_new_to_sweep_leaves_them()
        {
            //Arrange
            var timeSinceSent = TimeSpan.FromMilliseconds(500);

            var timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            var internalBus = new InternalBus();
            var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { MyTopic, new InMemoryProducer(internalBus, timeProvider) }
            });

            var bus = new ExternalBusService<Message, Transaction>(
                producerRegistry,
                new PolicyRegistry(),
                new MessageMapperRegistry(
                    new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                    new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())
                ),
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                outbox
            ); 

            var commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                bus);
            
            var sweeper = new OutboxSweeper(
                timeSinceSent, 
                commandProcessor, 
                new InMemoryRequestContextFactory());

            Message oldMessage = new MessageTestDataBuilder();
            commandProcessor.DepositPost(oldMessage.ToStubRequest());
            
            //delay so the previous message is old enough to sweep 
            timeProvider.Advance(timeSinceSent); 

            var messages = new Message[]
            {
                new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()
            };


            foreach (var message in messages)
            {
                commandProcessor.DepositPost(message.ToStubRequest());
            }

            //Act
            
            sweeper.Sweep();

            await Task.Delay(200); //Give the sweep time to run

            //Assert
            internalBus.Stream(new RoutingKey(MyTopic)).Count().Should().Be(3);
            outbox.OutstandingMessages(TimeSpan.Zero, new RequestContext()).Count().Should().Be(0);
        }

        [Fact]
        public async Task When_too_new_to_sweep_leaves_them_async()
        {
            //Arrange
            var timeSinceSent = TimeSpan.FromMilliseconds(500);

            var timeProvider = new FakeTimeProvider();
            
            var tracer = new BrighterTracer(timeProvider);
            var internalBus = new InternalBus();
            var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { MyTopic, new InMemoryProducer(internalBus, timeProvider) }
            });

            var bus = new ExternalBusService<Message, Transaction>(
                producerRegistry,
                new PolicyRegistry(),
                new MessageMapperRegistry(
                    new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                    new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())
                ),
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                outbox
            ); 

            var commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                bus);           
            
            var sweeper = new OutboxSweeper(timeSinceSent, commandProcessor, new InMemoryRequestContextFactory());

            Message oldMessage = new MessageTestDataBuilder();
            commandProcessor.DepositPost(oldMessage.ToStubRequest());

            var messages = new Message[]
            {
                new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()
            };

            //-- allow the messages to be old enough to sweep
            timeProvider.Advance(timeSinceSent); 

            foreach (var message in messages)
            {
                commandProcessor.DepositPost(message.ToStubRequest());
            }

            //Act
            sweeper.SweepAsyncOutbox();

            await Task.Delay(200); //Give the sweep time to run

            //Assert
            internalBus.Stream(new RoutingKey(MyTopic)).Count().Should().Be(3);
            outbox.OutstandingMessages(TimeSpan.Zero, new RequestContext()).Count().Should().Be(0);
        }
    }
}
