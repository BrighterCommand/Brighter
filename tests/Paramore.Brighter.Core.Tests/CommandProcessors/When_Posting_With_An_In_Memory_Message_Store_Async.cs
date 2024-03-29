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
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    [Collection("CommandProcessor")]
    public class CommandProcessorWithInMemoryOutboxAscyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly InMemoryOutbox _outbox;
        private readonly FakeMessageProducerWithPublishConfirmation _producer;

        public CommandProcessorWithInMemoryOutboxAscyncTests()
        {
            const string topic = "MyCommand";
            _myCommand.Value = "Hello World";

            _outbox = new InMemoryOutbox();
            _producer = new FakeMessageProducerWithPublishConfirmation{Publication = {Topic = new RoutingKey(topic), RequestType = typeof(MyCommand)}};

            _message = new Message(
                new MessageHeader(_myCommand.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync())
                );
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
                                                                                                                  
            var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

            var policyRegistry = new PolicyRegistry { { CommandProcessor.RETRYPOLICYASYNC, retryPolicy }, { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy } };
            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer> {{topic, _producer},});
            IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry, 
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                _outbox
            );

            CommandProcessor.ClearServiceBus();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                bus
            );
        }

        [Fact]
        public async Task When_Posting_With_An_In_Memory_Outbox_Async()
        {
            await _commandProcessor.PostAsync(_myCommand);

            var message = await _outbox.GetAsync(_myCommand.Id);
            //_should_store_the_message_in_the_sent_command_message_repository
            message.Should().NotBeNull();
            //_should_send_a_message_via_the_messaging_gateway
            _producer.MessageWasSent.Should().BeTrue();
            //_should_convert_the_command_into_a_message
            message.Should().Be(_message);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
