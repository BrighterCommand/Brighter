using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public interface IAmAnArchiveProvider
    {
        /// <summary>
        /// Send a Message to the archive provider
        /// </summary>
        /// <param name="message">Message to send</param>
        void ArchiveMessage(Message message);
        
        /// <summary>
        /// Send a Message to the archive provider
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        Task ArchiveMessageAsync(Message message, CancellationToken cancellationToken);
        
        /// <summary>
        /// Archive messages in Parallel
        /// </summary>
        /// <param name="messages">Messages to send</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>IDs of successfully archived messages</returns>
        Task<Guid[]> ArchiveMessagesAsync(Message[] messages, CancellationToken cancellationToken);
    }
}
