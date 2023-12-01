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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPostCommandTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly FakeOutbox _fakeOutbox;
        private readonly FakeMessageProducerWithPublishConfirmation _fakeMessageProducerWithPublishConfirmation;

        public CommandProcessorPostCommandTests()
        {
            _myCommand.Value = "Hello World";

            _fakeOutbox = new FakeOutbox();
            _fakeMessageProducerWithPublishConfirmation = new FakeMessageProducerWithPublishConfirmation();

            const string topic = "MyCommand";
            _message = new Message(
                new MessageHeader(_myCommand.Id, topic, MessageType.MT_COMMAND),
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
            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer> {{topic, _fakeMessageProducerWithPublishConfirmation},});
            IAmAnExternalBusService bus = new ExternalBusServices<Message, CommittableTransaction>(producerRegistry, policyRegistry, _fakeOutbox);

            CommandProcessor.ClearExtServiceBus();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                messageMapperRegistry,
                bus);
             }

        [Fact]
        public void When_Posting_A_Message_To_The_Command_Processor()
        {
            _commandProcessor.Post(_myCommand);

            //_should_store_the_message_in_the_message_store
            _fakeOutbox
                .Get()
                .SingleOrDefault(msg => msg.Id == _message.Id)
                .Should().NotBeNull();
            //_should_send_a_message_via_the_messaging_gateway
            _fakeMessageProducerWithPublishConfirmation.MessageWasSent.Should().BeTrue();
            //_should_convert_the_command_into_a_message
            _fakeOutbox.Get().First().Should().Be(_message);
        }

        public void Dispose()
        {
            CommandProcessor.ClearExtServiceBus();
        }
    }
}
