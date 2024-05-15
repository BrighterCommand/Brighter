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
    public class OutboxArchiver<TMessage, TTransaction> where TMessage: Message
    {
        private readonly IAmAnExternalBusService _bus;
        private const string ARCHIVE_OUTBOX = "Archive Outbox";
        
        private readonly ILogger _logger = ApplicationLogging.CreateLogger<OutboxArchiver<TMessage, TTransaction>>();
        
        private const string SUCCESS_MESSAGE = "Successfully archiver {NumberOfMessageArchived} out of {MessagesToArchive}, batch size : {BatchSize}";

        public OutboxArchiver(IAmAnExternalBusService bus)
        {
            _bus = bus;
        }

        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// Outbox Archiver will swallow any errors during the archive process, but record them. Assumption is
        /// that these are transient errors which can be retried
        /// </summary>
        /// <param name="minimumAge">Minimum age in hours</param>
        public void Archive(int minimumAge)
        {
            var activity = ApplicationTelemetry.ActivitySource.StartActivity(ARCHIVE_OUTBOX, ActivityKind.Server);

            try
            {
                _bus.Archive(minimumAge);  
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
        /// <param name="minimumAge">Minimum age in hours</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <param name="parallelArchiving">Send messages to archive provider in parallel</param>
        public async Task ArchiveAsync(int minimumAge, CancellationToken cancellationToken)
        {
            var activity = ApplicationTelemetry.ActivitySource.StartActivity(ARCHIVE_OUTBOX, ActivityKind.Server);
            
            try
            {
                await _bus.ArchiveAsync(minimumAge, cancellationToken);
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
