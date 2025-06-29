#region Licence
/* The MIT License (MIT)
Copyright © 2015 Toby Henderson <hendersont@gmail.com>

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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    /// <summary>
    /// A factory for creating an in-memory producer registry. This is mainly intended for usage with tests.
    /// It allows you to create a registry of in-memory producers that can be used to send messages to a bus.
    /// </summary>
    /// <param name="bus">An instance of <see cref="InternalBus"/> typically used for testing</param>
    /// <param name="publications">The list of topics that we want to publish to</param>
    /// <param name="instrumentationOptions">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
    public class InMemoryProducerRegistryFactory(InternalBus bus, IEnumerable<Publication> publications, InstrumentationOptions instrumentationOptions)
        : IAmAProducerRegistryFactory
    {
        /// <summary>
        /// Creates an in-memory producer registry.
        /// </summary>
        /// <returns>An instance of <see cref="IAmAProducerRegistry"/></returns>
        public IAmAProducerRegistry Create()
        {
            var producerFactory = new InMemoryMessageProducerFactory(bus, publications, instrumentationOptions);
            return new ProducerRegistry(producerFactory.Create());
        }

        /// <summary>
        /// Asynchronously creates an in-memory producer registry.
        /// </summary>
        /// <param name="ct">A cancellation token to cancel the operation</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an instance of <see cref="IAmAProducerRegistry"/></returns>
        public Task<IAmAProducerRegistry> CreateAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Create());
        }
    }
}
