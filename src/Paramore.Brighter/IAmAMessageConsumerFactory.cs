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

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAMessageConsumerFactory
    /// We do not know how to create a <see cref="IAmAMessageConsumer"/> implementation, as this knowledge belongs to the specific library for that broker.
    /// Implementors need to provide a concrete class to create instances of <see cref="IAmAMessageConsumer"/> for this library to use when building a <see cref="Channel"/>
    /// </summary>
    public interface IAmAMessageConsumerFactory
    {
        /// <summary>
        /// Creates the specified queue name.
        /// </summary>
        /// <param name="channelName">Name of the channel.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="isDurable">Is the consumer target durable i.e. channel stores messages between restarts of consumer</param>
        /// <param name="highAvailability">Does the queue exist in multiple nodes</param>
        /// <returns>IAmAMessageConsumer.</returns>
        IAmAMessageConsumer Create(string channelName, string routingKey, bool isDurable, bool highAvailability);
    }
}
