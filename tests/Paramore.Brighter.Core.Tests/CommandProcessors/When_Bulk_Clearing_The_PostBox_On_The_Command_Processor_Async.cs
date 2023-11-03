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
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    [Trait("Fragile", "CI")]
    [Collection("CommandProcessor")]
    public class CommandProcessorPostBoxBulkClearAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly Message _message;
        private readonly Message _message2;
        private readonly FakeOutbox _fakeOutbox;
        private readonly FakeMessageProducerWithPublishConfirmation _fakeMessageProducerWithPublishConfirmation;

        public CommandProcessorPostBoxBulkClearAsyncTests()
        {
            var myCommand = new MyCommand{ Value = "Hello World"};
            var myCommand2 = new MyCommand { Value = "Hello World 2" };

            _fakeOutbox = new FakeOutbox();
            _fakeMessageProducerWithPublishConfirmation = new FakeMessageProducerWithPublishConfirmation();

            var topic = "MyCommand";
            var topic2 = "MyCommand2";

            _message = new Message(
                new MessageHeader(myCommand.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
                );

            _message2 = new Message(
                new MessageHeader(myCommand.Id, topic2, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand2, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));
            
            var policyRegistry = new PolicyRegistry {{CommandProcessor.RETRYPOLICYASYNC, retryPolicy}, {CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy}};
            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { topic, _fakeMessageProducerWithPublishConfirmation },
                { topic2, _fakeMessageProducerWithPublishConfirmation }
            });
        
            IAmAnExternalBusService bus = new ExternalBusServices<Message, CommittableTransaction>(producerRegistry, policyRegistry, _fakeOutbox);
        
            CommandProcessor.ClearExtServiceBus();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(), 
                policyRegistry,
                messageMapperRegistry,
                bus);
        }

        
        [Fact(Skip = "Erratic due to timing")]
        public async Task When_Clearing_The_PostBox_On_The_Command_Processor_Async()
        {
            await _fakeOutbox.AddAsync(_message);
            await _fakeOutbox.AddAsync(_message2);

            _commandProcessor.ClearAsyncOutbox(2, 1, true);

            await Task.Delay(3000);

            //_should_send_a_message_via_the_messaging_gateway
            _fakeMessageProducerWithPublishConfirmation.MessageWasSent.Should().BeTrue();

            var sentMessage = _fakeMessageProducerWithPublishConfirmation.SentMessages[0];
            sentMessage.Should().NotBeNull();
            sentMessage.Id.Should().Be(_message.Id);
            sentMessage.Header.Topic.Should().Be(_message.Header.Topic);
            sentMessage.Body.Value.Should().Be(_message.Body.Value);

            var sentMessage2 = _fakeMessageProducerWithPublishConfirmation.SentMessages[1];
            sentMessage2.Should().NotBeNull();
            sentMessage2.Id.Should().Be(_message2.Id);
            sentMessage2.Header.Topic.Should().Be(_message2.Header.Topic);
            sentMessage2.Body.Value.Should().Be(_message2.Body.Value);
        }

        public void Dispose()
        {
            CommandProcessor.ClearExtServiceBus();
        }
    }
}

