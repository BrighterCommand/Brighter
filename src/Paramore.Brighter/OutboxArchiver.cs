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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

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
        private const string ARCHIVE_OUTBOX = "Archive Outbox";
        
        private readonly ILogger _logger = ApplicationLogging.CreateLogger<OutboxArchiver<TMessage, TTransaction>>();
        private readonly IAmARequestContextFactory _requestContextFactory = requestContextFactory ?? new InMemoryRequestContextFactory();

        private const string SUCCESS_MESSAGE = "Successfully archiver {NumberOfMessageArchived} out of {MessagesToArchive}, batch size : {BatchSize}";

        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// Outbox Archiver will swallow any errors during the archive process, but record them. Assumption is
        /// that these are transient errors which can be retried
        /// </summary>
        /// <param name="dispatchedSince">How stale is the message that we want archive</param>
        public void Archive(TimeSpan dispatchedSince)
        {
            var activity = ApplicationTelemetry.ActivitySource.StartActivity(ARCHIVE_OUTBOX, ActivityKind.Server);
            var requestContext = _requestContextFactory.Create();
            requestContext.Span = activity;
            
            try
            {
                bus.Archive(dispatchedSince, requestContext);  
            }
            catch (Exception e)
            {
                activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            }
            finally
            {
                if(activity?.DisplayName == ARCHIVE_OUTBOX)
                    activity.Dispose();
            }
        }

        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// Outbox Archiver will swallow any errors during the archive process, but record them. Assumption is
        /// that these are transient errors which can be retried       
        /// </summary>
        /// <param name="dispatchedSince">How stale is the message that</param>
        /// <param name="requestContext">The context for the request pipeline; gives us the OTel span for example</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        public async Task ArchiveAsync(TimeSpan dispatchedSince, RequestContext requestContext, CancellationToken cancellationToken)
        {
            var activity = ApplicationTelemetry.ActivitySource.StartActivity(ARCHIVE_OUTBOX, ActivityKind.Server);
            requestContext.Span = activity;
            
            try
            {
                await bus.ArchiveAsync(dispatchedSince, requestContext, cancellationToken);
            }
            catch (Exception e)
            {
                activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            }
            finally
            {
                if(activity?.DisplayName == ARCHIVE_OUTBOX)
                    activity.Dispose();
            }
        }
    }
}
