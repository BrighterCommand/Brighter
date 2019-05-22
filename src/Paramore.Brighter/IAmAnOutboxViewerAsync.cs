using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Inteface IAmAOutboxViewerAsync{T}
    /// Used to retrieve messages from an <see cref="IAmAnOutbox{T}"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IAmAnOutboxViewerAsync<T> where T : Message
    {
        /// <summary>
        ///  Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <returns></returns>
        Task<IList<T>> GetAsync(int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken));
         
    }
}
