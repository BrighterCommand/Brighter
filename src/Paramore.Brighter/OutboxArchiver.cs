using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    public class OutboxArchiver<TMessage, TTransaction> where TMessage: Message
    {
        private const string ARCHIVE_OUTBOX = "Archive Outbox";
        
        private readonly int _batchSize;
        private readonly IAmAnOutboxSync<TMessage, TTransaction> _outboxSync;
        private readonly IAmAnOutboxAsync<TMessage, TTransaction> _outboxAsync;
        private readonly IAmAnArchiveProvider _archiveProvider;
        private readonly ILogger _logger = ApplicationLogging.CreateLogger<OutboxArchiver<TMessage, TTransaction>>();
        
        private const string SUCCESS_MESSAGE = "Successfully archiver {NumberOfMessageArchived} out of {MessagesToArchive}, batch size : {BatchSize}";

        public OutboxArchiver(IAmAnOutbox outbox,IAmAnArchiveProvider archiveProvider, int batchSize = 100)
        {
            _batchSize = batchSize;
            if (outbox is IAmAnOutboxSync<TMessage, TTransaction> syncBox)
                _outboxSync = syncBox;
            
            if (outbox is IAmAnOutboxAsync<TMessage, TTransaction> asyncBox)
                _outboxAsync = asyncBox;

            _archiveProvider = archiveProvider;
        }

        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// </summary>
        /// <param name="minimumAge">Minimum age in hours</param>
        public void Archive(int minimumAge)
        {
            var activity = ApplicationTelemetry.ActivitySource.StartActivity(ARCHIVE_OUTBOX, ActivityKind.Server);

            try
            {
                var messages = _outboxSync.DispatchedMessages(minimumAge, _batchSize);

                if (!messages.Any()) return;
                foreach (var message in messages)
                {
                    _archiveProvider.ArchiveMessage(message);
                }

                _outboxSync.Delete(messages.Select(e => e.Id).ToArray());
                _logger.LogInformation(SUCCESS_MESSAGE, messages.Count(), messages.Count(), _batchSize);
            }
            catch (Exception e)
            {
                activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                _logger.LogError(e, "Error while archiving from the outbox");
                throw;
            }
            finally
            {
                if(activity?.DisplayName == ARCHIVE_OUTBOX)
                    activity.Dispose();
            }
        }
        
        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// </summary>
        /// <param name="minimumAge">Minimum age in hours</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <param name="parallelArchiving">Send messages to archive provider in parallel</param>
        public async Task ArchiveAsync(int minimumAge, CancellationToken cancellationToken, bool parallelArchiving = false)
        {
            var activity = ApplicationTelemetry.ActivitySource.StartActivity(ARCHIVE_OUTBOX, ActivityKind.Server);
            
            try
            {
                var messages = await _outboxAsync.DispatchedMessagesAsync(minimumAge, _batchSize,
                    cancellationToken);

                if (!messages.Any()) return;

                Guid[] successfullyArchivedMessages;
                if (parallelArchiving)
                {
                    successfullyArchivedMessages = await _archiveProvider.ArchiveMessagesAsync(messages.ToArray(), cancellationToken);
                }
                else
                {
                    foreach (var message in messages)
                    {
                        await _archiveProvider.ArchiveMessageAsync(message, cancellationToken);
                    }
                    successfullyArchivedMessages = messages.Select(m => m.Id).ToArray();
                }

                await _outboxAsync.DeleteAsync(messages.Select(e => e.Id).ToArray(), cancellationToken);
            }
            catch (Exception e)
            {
                activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                _logger.LogError(e, "Error while archiving from the outbox");
                throw;
            }
            finally
            {
                if(activity?.DisplayName == ARCHIVE_OUTBOX)
                    activity.Dispose();
            }
        }
    }
}
