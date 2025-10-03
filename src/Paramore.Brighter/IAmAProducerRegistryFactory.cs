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

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAProducerRegistryFactory
    /// </summary>
    public interface IAmAProducerRegistryFactory
    {
        /// <summary>
        /// Creates a message producer registry.
        /// </summary>
        /// <returns>A registry of middleware clients by topic, for sending messages to the middleware</returns>
        IAmAProducerRegistry Create();
        
        /// <summary>
        /// Creates a message producer registry.
        /// </summary>
        /// <remarks>
        /// Mainly useful where the producer creation is asynchronous, such as when connecting to a remote service to create or validate infrastructure
        /// </remarks>
        /// <param name="cancellationToken">A cancellation token to cancel the operation</param>
        /// <returns>A registry of middleware clients by topic, for sending messages to the middleware</returns>
        Task<IAmAProducerRegistry> CreateAsync(CancellationToken cancellationToken = default);
    }
}
