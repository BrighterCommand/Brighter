using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public interface IAmAnExternalBusService : IDisposable
    {
        /// <summary>
        /// Used with RPC to call a remote service via the external bus
        /// </summary>
        /// <param name="outMessage">The message to send</param>
        /// <typeparam name="T">The type of the call</typeparam>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        void CallViaExternalBus<T, TResponse>(Message outMessage)
            where T : class, ICall where TResponse : class, IResponse;
        
        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="posts">The ids of the posts that you would like to clear</param>
        /// <exception cref="InvalidOperationException">Thrown if there is no async outbox defined</exception>
        /// <exception cref="NullReferenceException">Thrown if a message cannot be found</exception>
        void ClearOutbox(params Guid[] posts);

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="posts">The ids of the posts that you would like to clear</param>
        /// <param name="continueOnCapturedContext">Should we use the same thread in the callback</param>
        /// <param name="cancellationToken">Allow cancellation of the operation</param>
        /// <exception cref="InvalidOperationException">Thrown if there is no async outbox defined</exception>
        /// <exception cref="NullReferenceException">Thrown if a message cannot be found</exception>
        Task ClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext,
            CancellationToken cancellationToken);

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="amountToClear">Maximum number to clear.</param>
        /// <param name="minimumAge">The minimum age of messages to be cleared in milliseconds.</param>
        /// <param name="useAsync">Use the Async outbox and Producer</param>
        /// <param name="useBulk">Use bulk sending capability of the message producer, this must be paired with useAsync.</param>
        /// <param name="args">Optional bag of arguments required by an outbox implementation to sweep</param>
        void ClearOutbox(int amountToClear, int minimumAge, bool useAsync, bool useBulk,
            Dictionary<string, object> args = null);

        /// <summary>
        /// Do we have an async outbox defined?
        /// </summary>
        /// <returns>true if defined</returns>
        bool HasAsyncOutbox();

        /// <summary>
        /// Do we have an async bulk outbox defined?
        /// </summary>
        /// <returns>true if defined</returns>
        bool HasAsyncBulkOutbox();

        /// <summary>
        /// Do we have a synchronous outbox defined?
        /// </summary>
        /// <returns>true if defined</returns>
        bool HasOutbox();

        /// <summary>
        /// Do we have a synchronous bulk outbox defined?
        /// </summary>
        /// <returns>true if defined</returns>
        bool HasBulkOutbox();

        /// <summary>
        /// Retry an action via the policy engine
        /// </summary>
        /// <param name="action">The Action to try</param>
        /// <returns></returns>
        bool Retry(Action action);
    }
}
