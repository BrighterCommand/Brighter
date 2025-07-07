#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmACommandProcessor
    /// Provides the interface for the command processor, which dispatches commands and events to handlers, invoking any required middleware. 
    /// Brighter provides the default implementation of this interface <see cref="CommandProcessor"/> and it is unlikely you need
    /// to override this for anything other than testing purposes. 
    /// The usual testing need is that in a <see cref="RequestHandler{T}"/> you intend to publish an <see cref="Event"/> to indicate the 
    /// handler has completed to other components. In this case your tests should only verify that the correct event was raised by 
    /// listening to <see cref="Publish{T}"/> calls on this interface, using a mocking framework of your choice or bespoke
    /// Test Double.
    /// </summary>
    public interface IAmACommandProcessor
    {
        /// <summary>
        /// Sends the specified command.
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        void Send<TRequest>(TRequest command, RequestContext? requestContext = null) where TRequest : class, IRequest;

        /// <summary>
        /// Scheduler a sends the specified command.
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="at">The date-time the message should be sent.</param>
        /// <param name="command">The command.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <returns>The scheduler id.</returns>
        /// <remarks>When you scheduler a send command sync the whole pipeline during executing the scheduler command will be sync as well.</remarks>
        string Send<TRequest>(DateTimeOffset at, TRequest command, RequestContext? requestContext = null)
            where TRequest : class, IRequest;

        /// <summary>
        /// Scheduler a sends the specified command.
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="delay">The amount of delay before send the message.</param>
        /// <param name="command">The command.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <returns>The scheduler id.</returns>
        /// <remarks>When you scheduler a send command sync the whole pipeline during executing the scheduler command will be sync as well.</remarks>
        string Send<TRequest>(TimeSpan delay, TRequest command, RequestContext? requestContext = null)
            where TRequest : class, IRequest;
        
        /// <summary>
        /// Awaitably sends the specified command.
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        Task SendAsync<TRequest>(TRequest command, RequestContext? requestContext = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest;
        
        /// <summary>
        /// Scheduler an awaitable sends the specified command.
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="at">The date-time the message should be sent.</param>
        /// <param name="command">The command.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>The scheduler id.</returns>
        /// <remarks>When you scheduler a send command async the whole pipeline during executing the scheduler command will be async as well.</remarks>
        Task<string> SendAsync<TRequest>(DateTimeOffset at, TRequest command, RequestContext? requestContext = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest;
        
        /// <summary>
        /// Scheduler an awaitable sends the specified command.
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="delay">The amount of delay before send the message.</param>
        /// <param name="command">The command.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>The scheduler id.</returns>
        /// <remarks>When you scheduler a send command async the whole pipeline during executing the scheduler command will be async as well.</remarks>
        Task<string> SendAsync<TRequest>(TimeSpan delay, TRequest command, RequestContext? requestContext = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest;

        /// <summary>
        /// Publishes the specified event. 
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="event">The event.</param>
        /// <exception cref="AggregateException">Throws an aggregate exception on failure of a pipeline but executes remaining.</exception>
        void Publish<TRequest>(TRequest @event, RequestContext? requestContext = null) where TRequest : class, IRequest;
        
        /// <summary>
        /// Scheduler a publishes the specified event. 
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="at">The date-time the message should be publish.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="event">The event.</param>
        /// <returns>The scheduler id.</returns>
        /// <remarks>When you scheduler a publish an event sync the whole pipeline during executing the scheduler event will be sync as well.</remarks>
        string Publish<TRequest>(DateTimeOffset at, TRequest @event, RequestContext? requestContext = null) where TRequest : class, IRequest;
        
        /// <summary>
        /// Scheduler a publishes the specified event. 
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="delay">The amount of delay before publish the message.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="event">The event.</param>
        /// <returns>The scheduler id.</returns>
        /// <remarks>When you scheduler a publish an event sync the whole pipeline during executing the scheduler event will be sync as well.</remarks>
        string Publish<TRequest>(TimeSpan delay, TRequest @event, RequestContext? requestContext = null) where TRequest : class, IRequest;

        /// <summary>
        /// Publishes the specified event with async/await support. 
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="event">The event.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        /// <exception cref="AggregateException">Throws an aggregate exception on failure of a pipeline but executes remaining.</exception>
        Task PublishAsync<TRequest>(TRequest @event, 
            RequestContext? requestContext = null,
            bool continueOnCapturedContext = true, 
            CancellationToken cancellationToken = default
            ) where TRequest : class, IRequest;
        
        /// <summary>
        /// Scheduler Publishes the specified event with async/await support. 
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="at">The date-time the message should be published.</param>
        /// <param name="event">The event.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>The scheduler id.</returns>
        /// <remarks>When you scheduler a publish an event async the whole pipeline during executing the scheduler event will be async as well.</remarks>
        Task<string> PublishAsync<TRequest>(
            DateTimeOffset at,
            TRequest @event, 
            RequestContext? requestContext = null,
            bool continueOnCapturedContext = true, 
            CancellationToken cancellationToken = default
            ) where TRequest : class, IRequest;
        
        /// <summary>
        /// Scheduler Publishes the specified event with async/await support. 
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="delay">The amount of delay before publish the message.</param>
        /// <param name="event">The event.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>The scheduler id.</returns>
        /// <remarks>When you scheduler a publish an event async the whole pipeline during executing the scheduler event will be async as well.</remarks>
        Task<string> PublishAsync<TRequest>(
            TimeSpan delay,
            TRequest @event, 
            RequestContext? requestContext = null,
            bool continueOnCapturedContext = true, 
            CancellationToken cancellationToken = default
            ) where TRequest : class, IRequest;

        /// <summary>
        /// Posts the specified request.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        void Post<TRequest>(TRequest request, RequestContext? requestContext= null, Dictionary<string, object>? args = null) where TRequest : class, IRequest;
        
        /// <summary>
        /// Scheduler a posts the specified request.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <param name="at">The date-time the message should be post.</param>
        /// <param name="request">The request.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <returns>The scheduler id.</returns>
        /// <remarks>When you scheduler a post request sync the whole pipeline during executing the scheduler event will be sync as well.</remarks>
        string Post<TRequest>(DateTimeOffset at, TRequest request, RequestContext? requestContext= null, Dictionary<string, object>? args = null) where TRequest : class, IRequest;

        /// <summary>
        /// Scheduler a posts the specified request.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <param name="delay">The amount of delay before publish the message.</param>
        /// <param name="request">The request.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <returns>The scheduler id.</returns>
        /// <remarks>When you scheduler a post request sync the whole pipeline during executing the scheduler event will be sync as well.</remarks>
        string Post<TRequest>(TimeSpan delay, TRequest request, RequestContext? requestContext= null, Dictionary<string, object>? args = null) where TRequest : class, IRequest;

        /// <summary>
        /// Posts the specified request with async/await support.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        Task PostAsync<TRequest>(
            TRequest request, 
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true, 
            CancellationToken cancellationToken = default
        ) where TRequest : class, IRequest;

        /// <summary>
        /// Scheduler a posts the specified request with async/await support.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <param name="at">The date-time the message should be post.</param>
        /// <param name="request">The request.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        /// <remarks>When you scheduler a post request async the whole pipeline during executing the scheduler event will be async as well.</remarks>
        Task<string> PostAsync<TRequest>(
            DateTimeOffset at,
            TRequest request, 
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true, 
            CancellationToken cancellationToken = default
        ) where TRequest : class, IRequest;
        
        /// <summary>
        /// Scheduler a posts the specified request with async/await support.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <param name="delay">The amount of delay before publish the message.</param>
        /// <param name="request">The request.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>The scheduler id.</returns>
        /// <remarks>When you scheduler a post request async the whole pipeline during executing the scheduler event will be async as well.</remarks>
        Task<string> PostAsync<TRequest>(
            TimeSpan delay,
            TRequest request, 
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true, 
            CancellationToken cancellationToken = default
        ) where TRequest : class, IRequest;

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="CommandProcessor.ClearOutbox"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        Id DepositPost<TRequest>(TRequest request, RequestContext? requestContext = null, Dictionary<string, object>? args = null) where TRequest : class, IRequest;

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="CommandProcessor.ClearOutbox"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="transactionProvider">If using an Outbox, the transaction provider for the Outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="batchId">The id of the deposit batch, if this isn't set items will be added to the outbox as they come in and not as a batch</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of transaction used by the outbox</typeparam>
        /// <returns></returns>
        Id DepositPost<TRequest, TTransaction>(
            TRequest request,
            IAmABoxTransactionProvider<TTransaction> transactionProvider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            string? batchId = null
            ) where TRequest : class, IRequest;

        /// <summary>
        /// Adds a messages into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutbox"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns>The Id of the Message that has been deposited.</returns>
        Id[] DepositPost<TRequest>(IEnumerable<TRequest> requests, RequestContext? requestContext, Dictionary<string, object>? args = null) where TRequest : class, IRequest;

        /// <summary>
        /// Adds a messages into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutbox"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="transactionProvider">If using an Outbox, the transaction provider for the Outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of transaction used by the outbox</typeparam>
        /// <returns>The Id of the Message that has been deposited.</returns>
        Id[] DepositPost<TRequest, TTransaction>(
            IEnumerable<TRequest> requests,
            IAmABoxTransactionProvider<TTransaction> transactionProvider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null
            ) where TRequest : class, IRequest;

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="CommandProcessor.ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        Task<Id> DepositPostAsync<TRequest>(
            TRequest request,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default
            ) where TRequest : class, IRequest;


        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="CommandProcessor.ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="transactionProvider">If using an Outbox, the transaction provider for the Outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <param name="batchId">The id of the deposit batch, if this isn't set items will be added to the outbox as they come in and not as a batch</param>
        /// <typeparam name="T">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of transaction used by the outbox</typeparam>
        /// <returns></returns>
        Task<Id> DepositPostAsync<T, TTransaction>(
            T request,
            IAmABoxTransactionProvider<TTransaction> transactionProvider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default,
            string? batchId = null
            ) where T : class, IRequest;

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        Task<Id[]> DepositPostAsync<TRequest>(
            IEnumerable<TRequest> requests,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default
            ) where TRequest : class, IRequest;

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="transactionProvider">If using an Outbox, the transaction provider for the Outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <typeparam name="T">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of transaction used by the outbox</typeparam>
        /// <returns></returns>
        Task<Id[]> DepositPostAsync<T, TTransaction>(
            IEnumerable<T> requests,
            IAmABoxTransactionProvider<TTransaction> transactionProvider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default
            ) where T : class, IRequest;

        /// <summary>
        /// Flushes the message box message given by <param name="ids"/> to the broker.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPost{TRequest}(TRequest,Paramore.Brighter.RequestContext?,System.Collections.Generic.Dictionary{string,object}?)"/>
        /// </summary>
        /// <param name="ids">The ids to flush</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        void ClearOutbox(Id[] ids, RequestContext? requestContext = null, Dictionary<string, object>? args = null);

        /// <summary>
        /// Flushes the message box message given by <param name="posts"/> to the broker.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPostAsync{TRequest}(TRequest,Paramore.Brighter.RequestContext?,System.Collections.Generic.Dictionary{string,object}?,bool,System.Threading.CancellationToken)"/>
        /// </summary>
        /// <param name="posts">The ids to flush</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext"></param>
        /// <param name="cancellationToken"></param>
        Task ClearOutboxAsync(
            IEnumerable<Id> posts,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Uses the Request-Reply messaging approach to send a message to another server and block awaiting a reply.
        /// The message is placed into a message queue but not into the outbox.
        /// An ephemeral reply queue is created, and its name used to set the reply address for the response. We produce
        /// a queue per exchange, to simplify correlating send and receive.
        /// The response is directed to a registered handler.
        /// Because the operation blocks, there is a mandatory timeout
        /// </summary>
        /// <param name="request">What message do we want a reply to</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/></param>
        /// <param name="timeOut">The call blocks, so we must time out; defaults to 500 ms if null</param>
        /// <exception cref="NotImplementedException"></exception>
        TResponse? Call<T, TResponse>(T request, RequestContext? requestContext = null, TimeSpan? timeOut = null)
            where T : class, ICall where TResponse : class, IResponse;
    }
}
