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
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    [Collection("CommandProcessor")]
    public class PostFailureLimitCommandTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly InMemoryOutbox _outbox;
        private readonly FakeTimeProvider _timeProvider;

        public PostFailureLimitCommandTests()
        {
            var routingKey = new RoutingKey("MyCommand");
            
            IAmAMessageProducer producer = new FakeErroringMessageProducerSync{Publication = { Topic = routingKey, RequestType = typeof(MyCommand)}};

            var messageMapperRegistry =
                new MessageMapperRegistry(
                    new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                    null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            _timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer();
            _outbox = new InMemoryOutbox(_timeProvider) {Tracer = tracer};

            var producerRegistry =
                new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer }, }); 
            
            var externalBus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry: producerRegistry,
                policyRegistry: new DefaultPolicy(),
                mapperRegistry: messageMapperRegistry,
                messageTransformerFactory: new EmptyMessageTransformerFactory(),
                messageTransformerFactoryAsync: new EmptyMessageTransformerFactoryAsync(),     
                tracer,
                outbox: _outbox,
                maxOutStandingMessages:3,
                maxOutStandingCheckInterval: TimeSpan.FromMilliseconds(250)
            );  
            
            _commandProcessor = CommandProcessorBuilder.StartNew()
                .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new EmptyHandlerFactorySync()))
                .DefaultPolicy()
                .ExternalBus(ExternalBusType.FireAndForget, externalBus)
                .ConfigureInstrumentation(new BrighterTracer(TimeProvider.System), InstrumentationOptions.All)
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();
        }

        [Fact]
        public async Task When_Posting_Fails_Limit_Total_Writes_To_OutBox_In_Window()
        {
            var sentList = new List<string>(); 
            bool shouldThrowException = false;
            try
            {
                do
                {
                    var command = new MyCommand{Value = $"Hello World: {sentList.Count() + 1}"};
                    _commandProcessor.Post(command);
                    sentList.Add(command.Id);
                    
                    _timeProvider.Advance(TimeSpan.FromMilliseconds(500));

                    //We need to wait for the sweeper thread to check the outstanding in the outbox
                    await Task.Delay(50);

                } while (sentList.Count < 10);
            }
            catch (OutboxLimitReachedException)
            {
                shouldThrowException = true;
            }
            
            //We should error before the end
            shouldThrowException.Should().BeTrue();
            
            //should store the message in the sent outbox
            foreach (var id in sentList)
            {
                _outbox.Get(id, new RequestContext()).Should().NotBeNull();
            }
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }

        internal class EmptyHandlerFactorySync : IAmAHandlerFactorySync
        {
            public IHandleRequests Create(Type handlerType, IAmALifetime lifetime)
            {
                return null;
            }

            public void Release(IHandleRequests handler, IAmALifetime lifetime) {}
        }
    }
}
