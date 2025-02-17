#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Clear
{
    [Trait("Fragile", "CI")]
    [Collection("CommandProcessor")]
    public class CommandProcessorPostBoxBulkClearAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly Message _messageOne;
        private readonly Message _messageTwo;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        private readonly IAmAnOutboxProducerMediator _mediator;

        public CommandProcessorPostBoxBulkClearAsyncTests()
        {
            var myCommand = new MyCommand{ Value = "Hello World"};
            var myCommand2 = new MyCommand { Value = "Hello World 2" };

            var timeProvider = new FakeTimeProvider();

            var routingKey = new RoutingKey("MyCommand");
            
            InMemoryProducer producer = new(_internalBus, timeProvider)
            {
                Publication = {Topic = routingKey, RequestType = typeof(MyCommand)}
            };

            var routingKeyTwo = new RoutingKey("MyCommand2");
            InMemoryProducer producerTwo = new(_internalBus, timeProvider)
            {
                Publication = {Topic = routingKeyTwo, RequestType = typeof(MyCommand)}
            };

            _messageOne = new Message(
                new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
                );

            _messageTwo = new Message(
                new MessageHeader(myCommand.Id, routingKeyTwo, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand2, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));
            
            var policyRegistry = new PolicyRegistry {{CommandProcessor.RETRYPOLICYASYNC, retryPolicy}, {CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy}};
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { routingKey, producer },
                { routingKeyTwo, producerTwo }
            });
            
            var tracer = new BrighterTracer();
            _outbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};

            _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry, 
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                _outbox
            );
        
            CommandProcessor.ClearServiceBus();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(), 
                policyRegistry,
                _mediator,
                requestSchedulerFactory: new InMemorySchedulerFactory()
            );
        }

        
        [Fact(Skip = "Erratic due to timing")]
        public async Task When_Clearing_The_PostBox_On_The_Command_Processor_Async()
        {
            var context = new RequestContext();
            await _outbox.AddAsync(_messageOne, context);
            await _outbox.AddAsync(_messageTwo, context);

            await _mediator.ClearOutstandingFromOutboxAsync(2, TimeSpan.FromMilliseconds(1), true, context);

            await Task.Delay(3000);

            //_should_send_a_message_via_the_messaging_gateway
            var routingKeyOne = new RoutingKey(_messageOne.Header.Topic);
            _internalBus.Stream(routingKeyOne).Any().Should().BeTrue();

            var sentMessage = _internalBus.Dequeue(routingKeyOne);
            sentMessage.Should().NotBeNull();
            sentMessage.Id.Should().Be(_messageOne.Id);
            sentMessage.Header.Topic.Should().Be(_messageOne.Header.Topic);
            sentMessage.Body.Value.Should().Be(_messageOne.Body.Value);

            var routingKeyTwo = new RoutingKey(_messageTwo.Header.Topic);
            _internalBus.Stream(routingKeyOne).Any().Should().BeTrue();
            
            var sentMessage2 = _internalBus.Dequeue(routingKeyTwo); 
            sentMessage2.Should().NotBeNull();
            sentMessage2.Id.Should().Be(_messageTwo.Id);
            sentMessage2.Header.Topic.Should().Be(_messageTwo.Header.Topic);
            sentMessage2.Body.Value.Should().Be(_messageTwo.Body.Value);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}

