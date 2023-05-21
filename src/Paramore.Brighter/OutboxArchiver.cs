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

        public OutboxArchiver(IAmAnOutbox outbox, IAmAnArchiveProvider archiveProvider, int batchSize = 100)
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
            var age = TimeSpan.FromHours(minimumAge);

            try
            {
                var messages = _outboxSync.DispatchedMessages(age.Milliseconds, _batchSize);

                if (!messages.Any()) return;
                foreach (var message in messages)
                {
                    _archiveProvider.ArchiveMessage(message);
                }

                _outboxSync.Delete(messages.Select(e => e.Id).ToArray());
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
        public async Task ArchiveAsync(int minimumAge, CancellationToken cancellationToken)
        {
            var activity = ApplicationTelemetry.ActivitySource.StartActivity(ARCHIVE_OUTBOX, ActivityKind.Server);
            
            var age = TimeSpan.FromHours(minimumAge);

            try
            {
                var messages = await _outboxAsync.DispatchedMessagesAsync(age.Milliseconds, _batchSize,
                    cancellationToken: cancellationToken);

                if (!messages.Any()) return;
                foreach (var message in messages)
                {
                    await _archiveProvider.ArchiveMessageAsync(message, cancellationToken);
                }

                await _outboxAsync.DeleteAsync(cancellationToken, messages.Select(e => e.Id).ToArray());
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
