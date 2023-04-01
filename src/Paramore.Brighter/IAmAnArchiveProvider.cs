using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public interface IAmAnArchiveProvider
    {
        void ArchiveMessage(Message message);

        Task ArchiveMessageAsync(Message message, CancellationToken cancellationToken);
    }
}
