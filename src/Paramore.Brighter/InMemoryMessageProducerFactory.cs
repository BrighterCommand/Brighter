#region Licence
/* The MIT License (MIT)
Copyright © 2024 Dominic Hickie <dominichickie@gmail.com>

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
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    /// <summary>
    /// A factory for creating a dictionary of in-memory producers indexed by topic. This is mainly intended for usage with tests.
    /// It allows you to send messages to a bus and then inspect the messages that have been sent.
    /// </summary>
    /// <param name="bus">An instance of <see cref="IAmABus"/> typically we use an <see cref="InternalBus"/></param>
    /// <param name="publications">The list of topics that we want to publish to</param>
    /// <param name="instrumentationOptions">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
    public class InMemoryMessageProducerFactory(InternalBus bus, IEnumerable<Publication> publications, InstrumentationOptions instrumentationOptions)
        : IAmAMessageProducerFactory
    {

        /// <summary>
        /// Creates a dictionary of message producers.
        /// </summary>
        /// <returns>A dictionary of <see cref="IAmAMessageProducer"/> indexed by <see cref="RoutingKey"/></returns>
        /// <exception cref="ConfigurationException">Thrown when a publication does not have a topic</exception>
        public Dictionary<ProducerKey, IAmAMessageProducer> Create()
        {
            var producers = new Dictionary<ProducerKey, IAmAMessageProducer>();
            foreach (var publication in publications)
            {
                if (publication.Topic is null)
                    throw new ConfigurationException("A publication must have a Topic to be dispatched");
                var producer = new InMemoryMessageProducer(bus, TimeProvider.System, instrumentationOptions:instrumentationOptions);
                producer.Publication = publication;
                var producerKey = new ProducerKey(publication.Topic, publication.Type);
                if (producers.ContainsKey(producerKey))
                    throw new ConfigurationException($"A publication with the topic {publication.Topic}  and {publication.Type} already exists in the producer registry. Each topic + type must be unique in the producer registry. If you did not set a type, we will match against an empty type, so you cannot have two publications with the same topic and no type in the producer registry.");
                producers[producerKey] = producer;
            }

            return producers;
        }

        /// <summary>
        /// Creates a dictionary of message producers.
        /// </summary>
        /// <returns>A dictionary of <see cref="IAmAMessageProducer"/> indexed by <see cref="RoutingKey"/></returns>
        /// <exception cref="ArgumentException">Thrown when a publication does not have a topic</exception>
        public Task<Dictionary<ProducerKey, IAmAMessageProducer>> CreateAsync()
        {
            return Task.FromResult(Create());
        }
    }
}
