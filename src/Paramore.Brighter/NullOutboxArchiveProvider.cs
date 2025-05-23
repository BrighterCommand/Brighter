#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    /// <summary>
    /// Use this archiver will result in messages just being deleted from the outbox and not stored
    /// </summary>
    public class NullOutboxArchiveProvider : IAmAnArchiveProvider
    {
        private readonly ILogger _logger = ApplicationLogging.CreateLogger<NullOutboxArchiveProvider>();

        /// <summary>
        /// Send a Message to the archive provider
        /// </summary>
        /// <param name="message">Message to send</param>
        public void ArchiveMessage(Message message)
        {
            _logger.LogDebug("Message with Id {MessageId} will not be stored", message.Id);
        }

        /// <summary>
        /// Send a Message to the archive provider
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        public Task ArchiveMessageAsync(Message message, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Message with Id {MessageId} will not be stored", message.Id);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Archive messages in Parallel
        /// </summary>
        /// <param name="messages">Messages to send</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>IDs of successfully archived messages</returns>
        public Task<Id[]> ArchiveMessagesAsync(Message[] messages, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Messages with Ids {MessageIds} will not be stored",
                string.Join(",", messages.Select(m => m.Id.ToString()).ToArray()));

            return Task.FromResult(messages.Select(m => m.Id).ToArray());
        }
    }
}
