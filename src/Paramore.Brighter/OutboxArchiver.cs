using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public class OutboxArchiver
    {
        private readonly int _batchSize;
        private IAmAnOutboxSync<Message> _outboxSync;
        private IAmAnOutboxAsync<Message> _outboxAsync;
        private IAmAnArchiveProvider _archiveProvider;

        public OutboxArchiver(IAmAnOutbox<Message> outbox,IAmAnArchiveProvider archiveProvider, int batchSize = 100)
        {
            _batchSize = batchSize;
            if (outbox is IAmAnOutboxSync<Message> syncBox)
                _outboxSync = syncBox;
            
            if (outbox is IAmAnOutboxAsync<Message> asyncBox)
                _outboxAsync = asyncBox;

            _archiveProvider = archiveProvider;
        }

        public void Archive(int minimumAge)
        {
            var messages = _outboxSync.DispatchedMessages(minimumAge, _batchSize);
            
            foreach (var message in messages)
            {
                _archiveProvider.ArchiveMessage(message);
            }
            
            _outboxSync.Delete(messages.Select(e => e.Id).ToArray());
        }
        
        public async Task ArchiveAsync(int minimumAge, CancellationToken cancellationToken)
        {
            var messages = await _outboxAsync.DispatchedMessagesAsync(minimumAge, _batchSize, cancellationToken: cancellationToken);

            foreach (var message in messages)
            {
                await _archiveProvider.ArchiveMessageAsync(message, cancellationToken);
            }
            
            await _outboxAsync.DeleteAsync(cancellationToken, messages.Select(e => e.Id).ToArray());
        }
    }
}
