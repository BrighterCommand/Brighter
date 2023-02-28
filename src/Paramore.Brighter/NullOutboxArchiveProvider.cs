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
        private ILogger _logger;
        
        public NullOutboxArchiveProvider()
        {
            _logger = ApplicationLogging.CreateLogger<NullOutboxArchiveProvider>();
        }
        public void ArchiveMessage(Message message)
        {
            _logger.LogDebug("Message with Id {MessageId} will not be stored", message.Id);
        }

        public Task ArchiveMessageAsync(Message message, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Message with Id {MessageId} will not be stored", message.Id);
            
            return Task.CompletedTask;
        }
    }
}
