using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// An external bus service allows us to send messages to external systems
    /// The interaction with the CommandProcessor is mostly via the Outbox and the Message Mapper
    /// </summary>
    public interface IAmAnOutboxProducerMediator : IDisposable
    {
        /// <summary>
        /// Used with RPC to call a remote service via the external bus
        /// </summary>
        /// <param name="outMessage">The message to send</param>
        /// <param name="requestContext">The context of the request pipeline</param>        
        /// <typeparam name="T">The type of the call</typeparam>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        void CallViaExternalBus<T, TResponse>(Message outMessage, RequestContext? requestContext)
            where T : class, ICall where TResponse : class, IResponse;

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="posts">The ids of the posts that you would like to clear</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <param name="args"></param>
        /// <exception cref="InvalidOperationException">Thrown if there is no async outbox defined</exception>
        /// <exception cref="NullReferenceException">Thrown if a message cannot be found</exception>
        void ClearOutbox(string[] posts, RequestContext requestContext, Dictionary<string, object>? args = null);

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="posts">The ids of the posts that you would like to clear</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <param name="continueOnCapturedContext">Should we use the same thread in the callback</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken">Allow cancellation of the operation</param>
        /// <exception cref="InvalidOperationException">Thrown if there is no async outbox defined</exception>
        /// <exception cref="NullReferenceException">Thrown if a message cannot be found</exception>
        Task ClearOutboxAsync(
            IEnumerable<string> posts,
            RequestContext requestContext,
            bool continueOnCapturedContext = true,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="amountToClear">Maximum number to clear.</param>
        /// <param name="minimumAge">The minimum age of messages to be cleared in milliseconds.</param>
        /// <param name="useBulk">Use bulk sending capability of the message producer, this must be paired with useAsync.</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <param name="args">Optional bag of arguments required by an outbox implementation to sweep</param>
        /// <param name="cancellationToken">Cancellation Token</param>
        Task ClearOutstandingFromOutboxAsync(int amountToClear,
            TimeSpan minimumAge,
            bool useBulk,
            RequestContext requestContext,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Given a request, run the transformation pipeline to create a message
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <typeparam name="TRequest">the type of the request</typeparam>
        /// <returns></returns>
        Message CreateMessageFromRequest<TRequest>(TRequest request, RequestContext requestContext)
            where TRequest : class, IRequest;

        /// <summary>
        /// Given a request, run the transformation pipeline to create a message 
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <param name="cancellationToken">Cancel the in-flight operation</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        Task<Message> CreateMessageFromRequestAsync<TRequest>(
            TRequest request,
            RequestContext requestContext,
            CancellationToken cancellationToken
        ) where TRequest : class, IRequest;

        /// <summary>
        /// Intended for usage with the CommandProcessor's Call method, this method will create a request from a message
        /// </summary>
        /// <param name="message">The message that forms a reply to a call</param>
        /// <param name="request">The request constructed from that message</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if there is no message mapper for the request</exception>
        void CreateRequestFromMessage<TRequest>(Message message, RequestContext? requestContext, out TRequest request)
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
    
    /// <summary>
    /// An external bus service allows us to send messages to external systems
    /// The interaction with the CommandProcessor is mostly via the Outbox and the Message Mapper
    /// </summary>
    public interface IAmAnOutboxProducerMediator<TMessage, TTransaction> : IAmAnOutboxProducerMediator
    {
        /// <summary>
        /// Adds a message to the outbox
        /// </summary>
        /// <param name="message">The message to store in the outbox</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <param name="overridingTransactionProvider">The provider of the transaction for the outbox</param>
        /// <param name="continueOnCapturedContext">Use the same thread for a callback</param>
        /// <param name="cancellationToken">Allow cancellation of the message</param>
        /// <param name="batchId">The id of the deposit batch, if this isn't set items will be added to the outbox as they come in and not as a batch</param>
        /// <typeparam name="TRequest">The type of request we are saving</typeparam>
        /// <exception cref="ChannelFailureException">Thrown if we cannot write to the outbox</exception>
        Task AddToOutboxAsync(
            TMessage message,
            RequestContext requestContext,
            IAmABoxTransactionProvider<TTransaction>? overridingTransactionProvider = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default,
            string? batchId = null);

        /// <summary>
        /// Adds a message to the outbox
        /// </summary>
        /// <param name="request">The request the message is composed from (used for diagnostics)</param>
        /// <param name="message">The message we intend to send</param>
        /// <param name="overridingTransactionProvider">A transaction provider that gives us the transaction to use with the Outbox</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <param name="batchId">The id of the deposit batch, if this isn't set items will be added to the outbox as they come in and not as a batch</param>
        /// <typeparam name="TRequest">The type of the request we have converted into a message</typeparam>
        /// <exception cref="ChannelFailureException">Thrown if we fail to write all the messages</exception>
        void AddToOutbox(
            TMessage message,
            RequestContext requestContext,
            IAmABoxTransactionProvider<TTransaction>? overridingTransactionProvider = null,
            string? batchId = null);
        
        /// <summary>
        /// Commence a batch of outbox messages to add
        /// </summary>
        /// <returns>The Id of the new batch</returns>
        string StartBatchAddToOutbox();

        /// <summary>
        /// Flush the batch of Messages to the outbox.
        /// </summary>
        /// <param name="batchId">The Id of the batch to be flushed</param>
        /// <param name="transactionProvider">The Transaction provider</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        void EndBatchAddToOutbox(string batchId, IAmABoxTransactionProvider<TTransaction>? transactionProvider, RequestContext requestContext);

        /// <summary>
        /// Flush the batch of Messages to the outbox.
        /// </summary>
        /// <param name="batchId">The Id of the batch to be flushed</param>
        /// <param name="transactionProvider">The Transaction provider</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/></param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <typeparam name="TTransaction"></typeparam>
        /// <returns>Awaitable Task</returns>
        Task EndBatchAddToOutboxAsync(string batchId,
            IAmABoxTransactionProvider<TTransaction>? transactionProvider, RequestContext requestContext,
            CancellationToken cancellationToken);
    }
}
