﻿#region Licence
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
    [Collection("CommandProcessor")]
    public class CommandProcessorPostBoxImplicitClearAsyncTests : IDisposable
    {
        private const string Topic = "MyCommand";
        private readonly CommandProcessor _commandProcessor;
        private readonly Message _message;
        private readonly Message _message2;
        private readonly FakeOutbox _fakeOutbox;
        private readonly InMemoryProducer _producer;
        private readonly InternalBus _bus = new();

        public CommandProcessorPostBoxImplicitClearAsyncTests()
        {
            var myCommand = new MyCommand{ Value = "Hello World"};

            var timeProvider = new FakeTimeProvider();
            _producer = new InMemoryProducer(_bus, timeProvider)
            {
                Publication = {Topic = new RoutingKey(Topic), RequestType = typeof(MyCommand)}
            };

            _message = new Message(
                new MessageHeader(myCommand.Id, Topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
                );

            _message2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), Topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
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

            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { Topic, _producer },
            });

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicy },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy }
            }; 
            
            _fakeOutbox = new FakeOutbox();
            
            IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                new BrighterTracer(timeProvider),
                _fakeOutbox
            );

            CommandProcessor.ClearServiceBus();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                bus
            );
        }

        [Fact]
        public async Task When_Implicit_Clearing_The_PostBox_On_The_Command_Processor_Async()
        {
            var context = new RequestContext();
            await _fakeOutbox.AddAsync(_message, context);
            await _fakeOutbox.AddAsync(_message2, context);

            _commandProcessor.ClearAsyncOutbox(1,1);

            for (var i = 1; i <= 10; i++)
            {
                if (_bus.Stream(new RoutingKey(Topic)).Count() == 1) break;
                await Task.Delay(i * 100);
            }

            _commandProcessor.ClearAsyncOutbox(1, 1);

            //Try again and kick off another background thread
            for (var i = 1; i <= 10; i++)
            {
                if (_bus.Stream(new RoutingKey(Topic)).Count() == 2)
                    break;
                await Task.Delay(i * 100);
                _commandProcessor.ClearAsyncOutbox(1, 1);
            }

            //_should_send_a_message_via_the_messaging_gateway
            var messages = _bus.Stream(new RoutingKey(Topic)).ToArray();
            messages.Any().Should().BeTrue();

            var sentMessage = messages.FirstOrDefault(m => m.Id == _message.Id);
            sentMessage.Should().NotBeNull();
            sentMessage?.Id.Should().Be(_message.Id);
            sentMessage?.Header.Topic.Should().Be(_message.Header.Topic);
            sentMessage?.Body.Value.Should().Be(_message.Body.Value);

            var sentMessage2 = messages.FirstOrDefault(m => m.Id == _message2.Id);
            sentMessage2.Should().NotBeNull();
            sentMessage2?.Id.Should().Be(_message2.Id);
            sentMessage2?.Header.Topic.Should().Be(_message2.Header.Topic);
            sentMessage2?.Body.Value.Should().Be(_message2.Body.Value);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
