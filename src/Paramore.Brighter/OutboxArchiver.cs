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

        public OutboxArchiver(IAmAnOutbox<Message> outbox, int batchSize = 100)
        {
            _batchSize = batchSize;
            if (outbox is IAmAnOutboxSync<Message> syncBox)
                _outboxSync = syncBox;
            
            if (outbox is IAmAnOutboxAsync<Message> asyncBox)
                _outboxAsync = asyncBox;
        }

        public void Archive(int minimumAge)
        {
            var messages = _outboxSync.DispatchedMessages(minimumAge, _batchSize);
            
            _outboxSync.Delete(messages.Select(e => e.Id).ToArray());
        }
        
        public async Task ArchiveAsync(int minimumAge, CancellationToken cancellationToken)
        {
            
        }
    }
}
