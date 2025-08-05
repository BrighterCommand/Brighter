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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPostCommandWithTransactionProviderTestsAsync : IDisposable
    {
        private const string Topic = "MyCommand";
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly Message _message;
        private readonly SpyOutbox _spyOutbox;
        private readonly SpyTransactionProvider _transactionProvider;
        private readonly InternalBus _internalBus = new();

        public CommandProcessorPostCommandWithTransactionProviderTestsAsync()
        {
            _myCommand.Value = "Hello World";

            var timeProvider = new FakeTimeProvider();
            var routingKey = new RoutingKey(Topic);
            
            InMemoryMessageProducer messageProducer = new(_internalBus, timeProvider, new Publication  {Topic = routingKey, RequestType = typeof(MyCommand)});

            _message = new Message(
                new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND),
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

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicy }, { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy }
            };
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> {{routingKey, messageProducer},});

            var tracer = new BrighterTracer(timeProvider);
            _spyOutbox = new SpyOutbox() {Tracer = tracer};
            _transactionProvider = new SpyTransactionProvider();
            
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, SpyTransaction>(
                producerRegistry, 
                policyRegistry, 
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                _spyOutbox
            );

            CommandProcessor.ClearServiceBus();
            var scheduler = new InMemorySchedulerFactory();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                bus,
                scheduler,
                _transactionProvider
            );
        }

        [Fact]
        public async Task When_Posting_A_Message_To_The_Command_Processor_With_A_Transaction_Provider_Configured_Async()
        {
            await _commandProcessor.PostAsync(_myCommand);

            //message should not be in the current transaction
            var transaction = _transactionProvider.GetTransaction();
            Assert.Null(transaction.Get(_myCommand.Id));

            //message should have been posted
            Assert.True(_internalBus.Stream(new RoutingKey(Topic)).Any());
            
            //message should be in the outbox
            var message = _spyOutbox.Get(_myCommand.Id, new RequestContext());
            Assert.NotNull(message);
            Assert.Equal(_message, message);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
