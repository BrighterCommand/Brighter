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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Used to archive messages from an Outbox
    /// </summary>
    /// <typeparam name="TMessage">The type of message to archive</typeparam>
    /// <typeparam name="TTransaction">The transaction type of the Db</typeparam>
    public class OutboxArchiver<TMessage, TTransaction>(
        IAmAnExternalBusService bus,
        IAmARequestContextFactory? requestContextFactory = null)
        where TMessage : Message
    {
        private readonly IAmARequestContextFactory _requestContextFactory = requestContextFactory ?? new InMemoryRequestContextFactory();

        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// Outbox Archiver will swallow any errors during the archive process, but record them. Assumption is
        /// that these are transient errors which can be retried
        /// </summary>
        /// <param name="dispatchedSince">How stale is the message that we want archive</param>
        /// <param name="requestContext">The context for the request pipeline; gives us the OTel span for example</param>
         public void Archive(TimeSpan dispatchedSince, RequestContext? requestContext = null)
        {
            requestContext ??= _requestContextFactory.Create();
             bus.Archive(dispatchedSince, requestContext);  
        }

        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// Outbox Archiver will swallow any errors during the archive process, but record them. Assumption is
        /// that these are transient errors which can be retried       
        /// </summary>
        /// <param name="dispatchedSince">How stale is the message that</param>
        /// <param name="requestContext">The context for the request pipeline; gives us the OTel span for example</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        public async Task ArchiveAsync(TimeSpan dispatchedSince, RequestContext? requestContext = null, CancellationToken cancellationToken = default)
        {
            requestContext ??= _requestContextFactory.Create();
            await bus.ArchiveAsync(dispatchedSince, requestContext, cancellationToken);
        }
    }
}
