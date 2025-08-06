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

using System.Transactions;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Mappers;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class ControlBusSenderFactory. Helper for creating instances of a control bus (which requires messaging, but not subscribers).
    /// </summary>
    public class ControlBusSenderFactory : IAmAControlBusSenderFactory
    {
        /// <summary>
        /// Creates the specified configuration.
        /// </summary>
        /// <param name="outbox">The outbox for outgoing messages to the control bus</param>
        /// <param name="producerRegistry"></param>
        /// <param name="tracer"></param>
        /// <param name="requestSchedulerFactory"></param>
        /// <returns>IAmAControlBusSender.</returns>
        public IAmAControlBusSender Create<T, TTransaction>(IAmAnOutbox outbox, 
            IAmAProducerRegistry producerRegistry,
            BrighterTracer tracer,
            IAmARequestSchedulerFactory? requestSchedulerFactory = null,
            IAmAPublicationFinder? publicationFinder = null)
            where T : Message
        {
            var mapper = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MonitorEventMessageMapper()),
                null);
            mapper.Register<MonitorEvent, MonitorEventMessageMapper>();

            var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry: producerRegistry,
                resiliencePipelineRegistry: new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
                mapperRegistry: mapper,
                messageTransformerFactory: new EmptyMessageTransformerFactory(),
                messageTransformerFactoryAsync: new EmptyMessageTransformerFactoryAsync(), tracer: tracer,
                outbox: outbox,
                outboxCircuitBreaker: new InMemoryOutboxCircuitBreaker(),
                publicationFinder: publicationFinder ?? new FindPublicationByPublicationTopicOrRequestType()
                ); 
            
            return new ControlBusSender(
                CommandProcessorBuilder.StartNew()
                .Handlers(new HandlerConfiguration())
                .DefaultResilience()
                .ExternalBus(ExternalBusType.FireAndForget, mediator)   
                .ConfigureInstrumentation(null, InstrumentationOptions.None)
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .RequestSchedulerFactory(requestSchedulerFactory ?? new InMemorySchedulerFactory())
                .Build()
                );
        }
    }
}
