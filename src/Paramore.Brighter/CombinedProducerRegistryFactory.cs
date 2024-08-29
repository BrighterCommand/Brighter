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

using System.Linq;

namespace Paramore.Brighter
{
    public class CombinedProducerRegistryFactory : IAmAProducerRegistryFactory
    {
        private readonly IAmAMessageProducerFactory[] _messageProducerFactories;

        /// <summary>
        /// Creates a combined producer registry of the message producers created by a set of message
        /// producer factories.
        /// </summary>
        /// <param name="messageProducerFactories">The set of message producer factories from which to create the combined registry</param>
        public CombinedProducerRegistryFactory(params IAmAMessageProducerFactory[] messageProducerFactories)
        {
            _messageProducerFactories = messageProducerFactories;
        }

        /// <summary>
        /// Create a combined producer registry of the producers created by the message producer factories,
        /// under the key of each topic
        /// </summary>
        /// <returns></returns>
        public IAmAProducerRegistry Create()
        {
            var producers = _messageProducerFactories
                .SelectMany(x => x.Create())
                .ToDictionary(x => x.Key, x => x.Value);
            return new ProducerRegistry(producers);
        }
    }
}
