using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public interface IAmAnArchiveProvider
    {
        void ArchiveMessage(Message message);
        
        Task<Guid?> ArchiveMessageAsync(Message message, CancellationToken cancellationToken);
        
        Task<Guid[]> ArchiveMessagesAsync(Message[] messages, CancellationToken cancellationToken);
    }
}
