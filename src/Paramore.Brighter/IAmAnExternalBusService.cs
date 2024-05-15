using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public interface IAmAnExternalBusService : IDisposable
    {
        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// </summary>
        /// <param name="millisecondsDispatchedSince">Minimum age in milliseconds</param>
        void Archive(int millisecondsDispatchedSince);

        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// </summary>
        /// <param name="millisecondsDispatchedSince"></param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        Task ArchiveAsync(int millisecondsDispatchedSince, CancellationToken cancellationToken);
        
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
        /// <param name="args"></param>
        /// <exception cref="InvalidOperationException">Thrown if there is no async outbox defined</exception>
        /// <exception cref="NullReferenceException">Thrown if a message cannot be found</exception>
        void ClearOutbox(string[] posts, Dictionary<string, object> args = null);

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="posts">The ids of the posts that you would like to clear</param>
        /// <param name="continueOnCapturedContext">Should we use the same thread in the callback</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken">Allow cancellation of the operation</param>
        /// <exception cref="InvalidOperationException">Thrown if there is no async outbox defined</exception>
        /// <exception cref="NullReferenceException">Thrown if a message cannot be found</exception>
        Task ClearOutboxAsync(IEnumerable<string> posts,
            bool continueOnCapturedContext = false,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="amountToClear">Maximum number to clear.</param>
        /// <param name="minimumAge">The minimum age of messages to be cleared in milliseconds.</param>
        /// <param name="useAsync">Use the Async outbox and Producer</param>
        /// <param name="useBulk">Use bulk sending capability of the message producer, this must be paired with useAsync.</param>
        /// <param name="args">Optional bag of arguments required by an outbox implementation to sweep</param>
        void ClearOutbox(
            int amountToClear,
            int minimumAge,
            bool useAsync,
            bool useBulk,
            Dictionary<string, object> args = null);

        /// <summary>
        /// Given a request, run the transformation pipeline to create a message
        /// </summary>
        /// <param name="request">The request</param>
        /// <typeparam name="TRequest">the type of the request</typeparam>
        /// <typeparam name="TTransaction"></typeparam>
        /// <returns></returns>
        Message CreateMessageFromRequest<TRequest>(TRequest request) where TRequest : class, IRequest;

        /// <summary>
        /// Given a request, run the transformation pipeline to create a message 
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="cancellationToken">Cancel the in-flight operation</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction"></typeparam>
        /// <returns></returns>
        Task<Message> CreateMessageFromRequestAsync<TRequest>(TRequest request,
            CancellationToken cancellationToken) where TRequest : class, IRequest;

        /// <summary>
        /// Given a set of messages, map them to requests
        /// </summary>
        /// <param name="requestType">The type of the request</param>
        /// <param name="requests">The list of requests</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<List<Message>> CreateMessagesFromRequests(
            Type requestType, 
            IEnumerable<IRequest> requests,
            CancellationToken cancellationToken);
        
        /// <summary>
        /// Intended for usage with the CommandProcessor's Call method, this method will create a request from a message
        /// </summary>
        /// <param name="message">The message that forms a reply to a call</param>
        /// <param name="request">The request constructed from that message</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if there is no message mapper for the request</exception>
        void CreateRequestFromMessage<TRequest>(Message message, out TRequest request)
            where TRequest : class, IRequest;

        /// <summary>
        /// Do we have an async outbox defined?
        /// </summary>
        /// <returns>true if defined</returns>
        bool HasAsyncOutbox();

        /// <summary>
        /// Do we have a synchronous outbox defined?
        /// </summary>
        /// <returns>true if defined</returns>
        bool HasOutbox();
    }
}
