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
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    [Collection("CommandProcessor")]
    public class ControlBusSenderPostMessageTests : IDisposable
    {
        private readonly ControlBusSender _controlBusSender;
        private readonly MyCommand _myCommand = new();
        private readonly Message _message;
        private readonly InMemoryOutbox _outbox;
        private readonly FakeTimeProvider _timeProvider;

        public ControlBusSenderPostMessageTests()
        {
            var routingKey = new RoutingKey("MyCommand");
            _myCommand.Value = "Hello World";

            _timeProvider = new FakeTimeProvider();
            InMemoryProducer producer = new(new InternalBus(), _timeProvider)
            {
                Publication = {Topic = routingKey, RequestType = typeof(MyCommand)}
            };

            _message = new Message(
                new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            var policyRegistry = new PolicyRegistry { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } };
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> {{routingKey, producer},});

            var tracer = new BrighterTracer(_timeProvider);
            _outbox = new InMemoryOutbox(_timeProvider) {Tracer = tracer};
            
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry, 
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                _outbox,
                timeProvider: _timeProvider
            );

            CommandProcessor.ClearServiceBus();
            CommandProcessor commandProcessor = new(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                bus,
                new InMemorySchedulerFactory()
            );

            _controlBusSender = new ControlBusSender(commandProcessor);
        }

        [Fact]
        public void When_Posting_Via_A_Control_Bus_Sender()
        {
            _controlBusSender.Post(_myCommand);
            
            _timeProvider.Advance(TimeSpan.FromSeconds(30));

            //_should_store_the_message_in_the_sent_command_message_repository
            var message = _outbox
              .DispatchedMessages(TimeSpan.FromSeconds(10), new RequestContext(), 1)
              .SingleOrDefault();
              
            message.Should().NotBeNull();
            
            //_should_convert_the_command_into_a_message
            message.Should().Be(_message);
        }

        public void Dispose()
        {
            _controlBusSender.Dispose();
            CommandProcessor.ClearServiceBus();
        }
    }
}
