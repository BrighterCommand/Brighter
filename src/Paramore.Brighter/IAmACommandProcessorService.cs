﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    // <summary>
    /// Interface IAmACommandProcessorService
    /// Paramore.Brighter provides the default implementation of this interface <see cref="CommandProcessorService"/> and it is unlikely you need
    /// to override this for anything other than testing purposes. The usual need is that in a <see cref="RequestHandler{T}"/> you intend to publish an  
    /// <see cref="Event"/> to indicate the handler has completed to other components. In this case your tests should only verify that the correct 
    /// event was raised by listening to <see cref="Publish{T}"/> calls on this interface, using a mocking framework of your choice or bespoke
    /// Test Double.
    /// </summary>
    public interface IAmACommandProcessorService : IDisposable
    {
        /// <summary>
        /// Sends the specified command.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="handlerFactory">The Handler Factory.</param>
        void Send<T>(T command, IAmAHandlerFactory handlerFactory) where T : class, IRequest;

        /// <summary>
        /// Awaitably sends the specified command.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="handlerFactory">The Handler Factory.</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        Task SendAsync<T>(T command, IAmAHandlerFactoryAsync handlerFactory, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest;

        /// <summary>
        /// Publishes the specified event. Throws an aggregate exception on failure of a pipeline but executes remaining
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event">The event.</param>
        /// <param name="handlerFactory">The Handler Factory.</param>
        void Publish<T>(T @event, IAmAHandlerFactory handlerFactory) where T : class, IRequest;

        /// <summary>
        /// Publishes the specified event with async/await support. Throws an aggregate exception on failure of a pipeline but executes remaining
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event">The event.</param>
        /// <param name="handlerFactory">The Handler Factory.</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        Task PublishAsync<T>(T @event, IAmAHandlerFactoryAsync handlerFactory,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest;

        /// <summary>
        /// Posts the specified request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        void Post<T>(T request) where T : class, IRequest;

        /// <summary>
        /// Posts the specified request with async/await support.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        Task PostAsync<T>(T request, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest;

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited Guid to <see cref="CommandProcessor.ClearOutbox"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <typeparam name="T">The type of the request</typeparam>
        /// <returns></returns>
        Guid DepositPost<T>(T request) where T : class, IRequest;

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited Guid to <see cref="CommandProcessor.ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <typeparam name="T">The type of the request</typeparam>
        /// <returns></returns>
        Task<Guid> DepositPostAsync<T>(T request, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest;

        /// <summary>
        /// Flushes the message box message given by <param name="posts"> to the broker.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPostBox"/>
        /// <param name="posts">The posts to flush</param>
        void ClearOutbox(params Guid[] posts);

        /// <summary>
        /// Flushes the message box message given by <param name="posts"> to the broker.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPostBoxAsync"/>
        /// <param name="posts">The posts to flush</param>
        Task ClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Uses the Request-Reply messaging approach to send a message to another server and block awaiting a reply.
        /// The message is placed into a message queue but not into the outbox.
        /// An ephemeral reply queue is created, and its name used to set the reply address for the response. We produce
        /// a queue per exchange, to simplify correlating send and receive.
        /// The response is directed to a registered handler.
        /// Because the operation blocks, there is a mandatory timeout
        /// </summary>
        /// <param name="request">What message do we want a reply to</param>
        /// <param name="timeOutInMilliseconds">The call blocks, so we must time out</param>
        /// <exception cref="NotImplementedException"></exception>
        TResponse Call<T, TResponse>(T request, int timeOutInMilliseconds)
            where T : class, ICall where TResponse : class, IResponse;
    }
}
