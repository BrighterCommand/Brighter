#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading.Tasks;

namespace Paramore.Brighter.ServiceActivator
{
    /// Abstracts the thread that runs a message pump
    public class Performer : IAmAPerformer
    {
        private readonly IAmAChannel _channel;
        private readonly IAmAMessagePump _messagePump;

        /// <summary>
        /// Constructs a performer, a combination of a message pump and a channel that it reads from
        /// A peformer is a single thread, increase the number of performs to increase the number of threads
        /// </summary>
        /// <param name="channel">The channel to read messages from</param>
        /// <param name="messagePump">The message pump that reads messages</param>
        public Performer(IAmAChannel channel, IAmAMessagePump messagePump)
        {
            _channel = channel;
            _messagePump = messagePump;
        }

        /// <summary>
        /// Stops a performer, by posting a quit message to the channel
        /// </summary>
        /// <param name="routingKey">The topic to post the quit message too</param>
        public void Stop(RoutingKey routingKey)
        {
            _channel.Stop(routingKey);
        }

        /// <summary>
        /// Runs this instance.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Run()
        {
            await Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Shut this performer and clean up its resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Shut this performer and clean up its resources
        /// </summary>
        ~Performer()
        {
            Dispose(false);
        }


        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _channel.Dispose();
            }
        }
    }
}
