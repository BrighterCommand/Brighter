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
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    /// <summary>
    /// Used to archive messages from an Outbox
    /// </summary>
    /// <typeparam name="TMessage">The type of message to archive</typeparam>
    /// <typeparam name="TTransaction">The transaction type of the Db</typeparam>
    public partial class OutboxArchiver<TMessage, TTransaction> where TMessage : Message
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<OutboxArchiver<TMessage, TTransaction>>();
        private readonly IAmARequestContextFactory _requestContextFactory;
        private readonly IAmAnOutboxSync<TMessage, TTransaction>? _outBox;
        private readonly IAmAnOutboxAsync<TMessage, TTransaction>? _asyncOutbox;
        private readonly IAmAnArchiveProvider _archiveProvider;
        private readonly int _archiveBatchSize;
        private readonly IAmABrighterTracer? _tracer;
        private readonly InstrumentationOptions _instrumentationOptions;

        /// <summary>
        /// Used to archive messages from an Outbox
        /// </summary>
        /// <typeparam name="TMessage">The type of message to archive</typeparam>
        /// <typeparam name="TTransaction">The transaction type of the Db</typeparam>
        public OutboxArchiver(
            IAmAnOutbox outbox,
            IAmAnArchiveProvider archiveProvider,
            IAmARequestContextFactory? requestContextFactory = null,
            int archiveBatchSize = 100,
            IAmABrighterTracer? tracer = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
            _archiveProvider = archiveProvider;
            _archiveBatchSize = archiveBatchSize;
            _tracer = tracer;
            _instrumentationOptions = instrumentationOptions;
            _requestContextFactory = requestContextFactory ?? new InMemoryRequestContextFactory();
            
            if (outbox is IAmAnOutboxSync<TMessage, TTransaction> syncOutbox) _outBox = syncOutbox;
            if (outbox is IAmAnOutboxAsync<TMessage, TTransaction> asyncOutbox) _asyncOutbox = asyncOutbox;
        }

        private const string NoSyncOutboxError = "A sync Outbox must be defined.";
        private const string NoArchiveProviderError = "An Archive Provider must be defined.";
        private const string NoAsyncOutboxError = "An async Outbox must be defined.";

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
            //This is an archive span parent; we expect individual archiving operations for messages to have their own spans
            var parentSpan = requestContext.Span;
            var span = _tracer?.CreateArchiveSpan(requestContext.Span, dispatchedSince, options: _instrumentationOptions);
            requestContext.Span = span;
            
            try
            {
                if (_outBox is null) throw new ArgumentException(NoSyncOutboxError);
                if (_archiveProvider is null) throw new ArgumentException(NoArchiveProviderError);
                var messages = _outBox
                    .DispatchedMessages(dispatchedSince, requestContext, _archiveBatchSize)
                    .ToArray();

                Log.FoundMessagesToArchive(s_logger, messages.Length, _archiveBatchSize);

                if (messages.Length <= 0) return;

                foreach (var message in messages)
                {
                    _archiveProvider.ArchiveMessage(message);
                }

                _outBox.Delete(messages.Select(e => e.Id).ToArray(), requestContext);

                Log.SuccessfullyArchivedMessages(s_logger, messages.Length, _archiveBatchSize);
            }
            catch (Exception e)
            {
                Log.ErrorArchivingFromOutbox(s_logger, e);
                _tracer?.AddExceptionToSpan(span, [e]);
                throw;
            }
            finally
            {
                _tracer?.EndSpan(span);
                requestContext.Span = parentSpan;
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
        public async Task ArchiveAsync(TimeSpan dispatchedSince, RequestContext? requestContext = null, CancellationToken cancellationToken = default)
        {
            requestContext ??= _requestContextFactory.Create();
            //This is an archive span parent; we expect individual archiving operations for messages to have their own spans
            var parentSpan = requestContext.Span;
            var span = _tracer?.CreateArchiveSpan(requestContext.Span, dispatchedSince, options: _instrumentationOptions);
            requestContext.Span = span;
            
            try
            {
                if (_asyncOutbox is null) throw new ArgumentException(NoAsyncOutboxError);
                if (_archiveProvider is null) throw new ArgumentException(NoArchiveProviderError);
                var messages = (await _asyncOutbox.DispatchedMessagesAsync(
                    dispatchedSince, requestContext, pageSize: _archiveBatchSize,
                    cancellationToken: cancellationToken
                )).ToArray();

                if (messages.Length <= 0)
                {
                }
                else
                {
                    foreach (var message in messages)
                    {
                        await _archiveProvider.ArchiveMessageAsync(message, cancellationToken);
                    }

                    await _asyncOutbox.DeleteAsync(messages.Select(e => e.Id).ToArray(), requestContext,
                        cancellationToken: cancellationToken
                    );
                }
            }
            catch (Exception e)
            {
                Log.ErrorArchivingFromOutbox(s_logger, e);
                _tracer?.AddExceptionToSpan(span, [e]);
                throw;
            }
            finally
            {
                _tracer?.EndSpan(span);
                requestContext.Span = parentSpan;
            }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "Found {NumberOfMessageArchived} message to archive, batch size : {BatchSize}")]
            public static partial void FoundMessagesToArchive(ILogger logger, int numberOfMessageArchived, int batchSize);

            [LoggerMessage(LogLevel.Information, "Successfully archived {NumberOfMessageArchived}, batch size : {BatchSize}")]
            public static partial void SuccessfullyArchivedMessages(ILogger logger, int numberOfMessageArchived, int batchSize);

            [LoggerMessage(LogLevel.Error, "Error while archiving from the outbox")]
            public static partial void ErrorArchivingFromOutbox(ILogger logger, Exception ex);
        }
    }
}

