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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Logging;
using Polly;
using Polly.Registry;
using Exception = System.Exception;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class CommandProcessor.
    /// Implements both the <a href="http://www.hillside.net/plop/plop2001/accepted_submissions/PLoP2001/bdupireandebfernandez0/PLoP2001_bdupireandebfernandez0_1.pdf">Command Dispatcher</a> 
    /// and <a href="http://wiki.hsr.ch/APF/files/CommandProcessor.pdf">Command Processor</a> Design Patterns 
    /// </summary>
    public class CommandProcessor : IAmACommandProcessor
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CommandProcessor>();

        private readonly IAmASubscriberRegistry _subscriberRegistry;
        private readonly IAmAHandlerFactorySync _handlerFactorySync;
        private readonly IAmAHandlerFactoryAsync _handlerFactoryAsync;
        private readonly IAmARequestContextFactory _requestContextFactory;
        private readonly IPolicyRegistry<string> _policyRegistry;
        private readonly InboxConfiguration _inboxConfiguration;
        private readonly IAmAFeatureSwitchRegistry _featureSwitchRegistry;
        private readonly IEnumerable<Subscription> _replySubscriptions;

        //Uses -1 to indicate no outbox and will thus force a throw on a failed publish

        // the following are not readonly to allow setting them to null on dispose
        private readonly IAmAChannelFactory _responseChannelFactory;

        private const string CREATEEVENT = "Cloud Events Create: {0}";
        private const string CLEAROUTBOX = "Clear Outbox";
        private const string PROCESSCOMMAND = "Process Command Type: {0}";
        private const string PROCESSEVENT = "Process Event Type: {0}";

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines for how long to break the circuit when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string CIRCUITBREAKER = "Paramore.Brighter.CommandProcessor.CircuitBreaker";

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines the retry strategy when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string RETRYPOLICY = "Paramore.Brighter.CommandProcessor.RetryPolicy";

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines for how long to break the circuit when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string CIRCUITBREAKERASYNC = "Paramore.Brighter.CommandProcessor.CircuitBreaker.Async";

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines the retry strategy when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string RETRYPOLICYASYNC = "Paramore.Brighter.CommandProcessor.RetryPolicy.Async";

        /// <summary>
        /// We want to use double lock to let us pass parameters to the constructor from the first instance
        /// </summary>
        private static IAmAnExternalBusService s_bus = null;
        private static readonly object s_padlock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class
        /// NO EXTERNAL BUS: Use this constructor when no external bus is required
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string> policyRegistry,
            IAmAFeatureSwitchRegistry featureSwitchRegistry = null,
            InboxConfiguration inboxConfiguration = null)
        {
            _subscriberRegistry = subscriberRegistry;

            if (HandlerFactoryIsNotEitherIAmAHandlerFactorySyncOrAsync(handlerFactory))
                throw new ArgumentException(
                    "No HandlerFactory has been set - either an instance of IAmAHandlerFactorySync or IAmAHandlerFactoryAsync needs to be set");

            if (handlerFactory is IAmAHandlerFactorySync handlerFactorySync)
                _handlerFactorySync = handlerFactorySync;
            if (handlerFactory is IAmAHandlerFactoryAsync handlerFactoryAsync)
                _handlerFactoryAsync = handlerFactoryAsync;

            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _featureSwitchRegistry = featureSwitchRegistry;
            _inboxConfiguration = inboxConfiguration;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// EXTERNAL BUS AND INTERNAL BUS: Use this constructor when both external bus and command processor support is required
        /// OPTIONAL RPC: You can use this if you want to use the command processor as a client to an external bus, but also want to support RPC
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="bus">The external service bus that we want to send messages over</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        /// <param name="replySubscriptions">The Subscriptions for creating the reply queues</param>
        /// <param name="responseChannelFactory">If we are expecting a response, then we need a channel to listen on</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string> policyRegistry,
            IAmAnExternalBusService bus,
            IAmAFeatureSwitchRegistry featureSwitchRegistry = null,
            InboxConfiguration inboxConfiguration = null,
            IEnumerable<Subscription> replySubscriptions = null,
            IAmAChannelFactory responseChannelFactory = null
            )
            : this(subscriberRegistry, handlerFactory, requestContextFactory, policyRegistry, featureSwitchRegistry, inboxConfiguration)
        {
            _responseChannelFactory = responseChannelFactory;
            _replySubscriptions = replySubscriptions;

            InitExtServiceBus(bus); 
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// EXTERNAL BUS, NO INTERNAL BUS: Use this constructor when only posting messages to an external bus is required
        /// </summary>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="bus">The external service bus that we want to send messages over</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        /// <param name="replySubscriptions">The Subscriptions for creating the reply queues</param>
        public CommandProcessor(
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string> policyRegistry,
            IAmAnExternalBusService bus,
            IAmAFeatureSwitchRegistry featureSwitchRegistry = null,
            InboxConfiguration inboxConfiguration = null,
            IEnumerable<Subscription> replySubscriptions = null)
        {
            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _featureSwitchRegistry = featureSwitchRegistry;
            _inboxConfiguration = inboxConfiguration;
            _replySubscriptions = replySubscriptions;
            
            InitExtServiceBus(bus); 
        }

        /// <summary>
        /// Sends the specified command. We expect only one handler. The command is handled synchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <exception cref="System.ArgumentException">
        /// </exception>
        public void Send<T>(T command, RequestContext requestContext = null) where T : class, IRequest
        {
            if (_handlerFactorySync == null)
                throw new InvalidOperationException("No handler factory defined.");

            var span = CreateSpan(string.Format(PROCESSCOMMAND, typeof(T).Name));
            var context = InitRequestContext(span, requestContext);

            using var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactorySync, _inboxConfiguration);
            try
            {
                s_logger.LogInformation("Building send pipeline for command: {CommandType} {Id}", command.GetType(),
                    command.Id);
                var handlerChain = builder.Build(context);

                AssertValidSendPipeline(command, handlerChain.Count());

                handlerChain.First().Handle(command);
            }
            catch (Exception)
            {
                span?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Sends the specified command.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task SendAsync<T>(
            T command, 
            RequestContext requestContext = null, 
            bool continueOnCapturedContext = false, 
            CancellationToken cancellationToken = default
        )
            where T : class, IRequest
        {
            if (_handlerFactoryAsync == null)
                throw new InvalidOperationException("No async handler factory defined.");

            var span = CreateSpan(string.Format(PROCESSCOMMAND, typeof(T).Name));
            var context = InitRequestContext(span, requestContext);

            using var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactoryAsync, _inboxConfiguration);
            try
            {
                s_logger.LogInformation("Building send async pipeline for command: {CommandType} {Id}",
                    command.GetType(), command.Id);
                var handlerChain = builder.BuildAsync(context, continueOnCapturedContext);

                AssertValidSendPipeline(command, handlerChain.Count());

                await handlerChain.First().HandleAsync(command, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext);
            }
            catch (Exception)
            {
                span?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Publishes the specified event. We expect zero or more handlers. The events are handled synchronously, in turn
        /// Because any pipeline might throw, yet we want to execute the remaining handler chains,  we catch exceptions on any publisher
        /// instead of stopping at the first failure and then we throw an AggregateException if any of the handlers failed, 
        /// with the InnerExceptions property containing the failures.
        /// It is up the implementer of the handler that throws to take steps to make it easy to identify the handler that threw.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event">The event.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        public void Publish<T>(T @event, RequestContext requestContext = null) where T : class, IRequest
        {
            if (_handlerFactorySync == null)
                throw new InvalidOperationException("No handler factory defined.");

            var span = CreateSpan(string.Format(PROCESSEVENT, typeof(T).Name));
            var context = InitRequestContext(span, requestContext);

            try
            {
                using var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactorySync, _inboxConfiguration);
                s_logger.LogInformation("Building send pipeline for event: {EventType} {Id}", @event.GetType(),
                    @event.Id);
                var handlerChain = builder.Build(context);

                var handlerCount = handlerChain.Count();

                s_logger.LogInformation("Found {HandlerCount} pipelines for event: {EventType} {Id}", handlerCount,
                   @event.GetType(), @event.Id);

                var exceptions = new List<Exception>();
                foreach (var handleRequests in handlerChain)
                {
                    try
                    {
                        handleRequests.Handle(@event);
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }

                if (exceptions.Any())
                {
                    span?.SetStatus(ActivityStatusCode.Error);
                    throw new AggregateException(
                        "Failed to publish to one more handlers successfully, see inner exceptions for details",
                        exceptions);
                }
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Publishes the specified event. We expect zero or more handlers. The events are handled synchronously and concurrently
        /// Because any pipeline might throw, yet we want to execute the remaining handler chains,  we catch exceptions on any publisher
        /// instead of stopping at the first failure and then we throw an AggregateException if any of the handlers failed, 
        /// with the InnerExceptions property containing the failures.
        /// It is up the implementer of the handler that throws to take steps to make it easy to identify the handler that threw.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event">The event.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task PublishAsync<T>(
            T @event,
            RequestContext requestContext = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default)
            where T : class, IRequest
        {
            if (_handlerFactoryAsync == null)
                throw new InvalidOperationException("No async handler factory defined.");

            var span = CreateSpan(string.Format(PROCESSEVENT, typeof(T).Name));
            var context = InitRequestContext(span, requestContext);

            using var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactoryAsync, _inboxConfiguration);
            try
            {
                s_logger.LogInformation("Building send async pipeline for event: {EventType} {Id}", @event.GetType(),
                    @event.Id);

                var handlerChain = builder.BuildAsync(context, continueOnCapturedContext);
                var handlerCount = handlerChain.Count();

                s_logger.LogInformation("Found {0} async pipelines for event: {EventType} {Id}", handlerCount,
                    @event.GetType(), @event.Id
                );

                var exceptions = new List<Exception>();
                foreach (var handler in handlerChain)
                {
                    try
                    {
                        await handler.HandleAsync(@event, cancellationToken).ConfigureAwait(continueOnCapturedContext);
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }

                if (exceptions.Any())
                    span?.SetStatus(ActivityStatusCode.Error);

                if (exceptions.Count > 0)
                {
                    throw new AggregateException(
                        "Failed to async publish to one more handlers successfully, see inner exceptions for details",
                        exceptions);
                }
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Posts the specified request. The message is placed on a task queue and into a outbox for reposting in the event of failure.
        /// You will need to configure a service that reads from the task queue to process the message
        /// Paramore.Brighter.ServiceActivator provides an endpoint for use in a windows service that reads from a queue
        /// and then Sends or Publishes the message to a <see cref="CommandProcessor"/> within that service. The decision to <see cref="Send{T}"/> or <see cref="Publish{T}"/> is based on the
        /// mapper. Your mapper can map to a <see cref="Message"/> with either a <see cref="T:MessageType.MT_COMMAND"/> , which results in a <see cref="Send{T}"/> or a
        /// <see cref="T:MessageType.MT_EVENT"/> which results in a <see cref="Publish{T}"/>
        /// Please note that this call will not participate in any ambient Transactions, if you wish to have the outbox participate in a Transaction please Use Deposit,
        /// and then after you have committed your transaction use ClearOutbox
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of request</typeparam>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public void Post<TRequest>(
            TRequest request, 
            RequestContext requestContext = null, 
            Dictionary<string, object> args = null
        ) where TRequest : class, IRequest
        {
            ClearOutbox(new []{DepositPost(request, (IAmABoxTransactionProvider<CommittableTransaction>)null, requestContext, args)}, requestContext, args);
        }

        /// <summary>
        /// Posts the specified request. The message is placed on a task queue and into a outbox for reposting in the event of failure.
        /// You will need to configure a service that reads from the task queue to process the message
        /// Paramore.Brighter.ServiceActivator provides an endpoint for use in a windows service that reads from a queue
        /// and then Sends or Publishes the message to a <see cref="CommandProcessor"/> within that service. The decision to <see cref="Send{T}"/> or <see cref="Publish{T}"/> is based on the
        /// mapper. Your mapper can map to a <see cref="Message"/> with either a <see cref="T:MessageType.MT_COMMAND"/> , which results in a <see cref="Send{T}"/> or a
        /// <see cref="T:MessageType.MT_EVENT"/> which results in a <see cref="Publish{T}"/>
        /// Please note that this call will not participate in any ambient Transactions, if you wish to have the outbox participate in a Transaction please Use DepositAsync,
        /// and then after you have committed your transaction use ClearOutboxAsync
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <typeparam name="TRequest">The type of request</typeparam>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task PostAsync<TRequest>(
            TRequest request,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default
            )
            where TRequest : class, IRequest
        {
            var messageId = await DepositPostAsync(request, (IAmABoxTransactionProvider<CommittableTransaction>)null, requestContext, args, continueOnCapturedContext, cancellationToken);
            await ClearOutboxAsync(new[] { messageId }, requestContext, args, continueOnCapturedContext, cancellationToken);
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutbox"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns>The Id of the Message that has been deposited.</returns>
        public string DepositPost<TRequest>(TRequest request, RequestContext requestContext = null, Dictionary<string, object> args = null) 
            where TRequest : class, IRequest
        {
            return DepositPost<TRequest, CommittableTransaction>(request, null, requestContext, args); 
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutbox"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="transactionProvider">The transaction provider to use with an outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of Db transaction used by the Outbox</typeparam>
        /// <returns>The Id of the Message that has been deposited.</returns>
        public string DepositPost<TRequest, TTransaction>(
            TRequest request,
            IAmABoxTransactionProvider<TTransaction> transactionProvider,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null) 
            where TRequest : class, IRequest
        {
            s_logger.LogInformation("Save request: {RequestType} {Id}", request.GetType(), request.Id);
            
            var span = CreateSpan(string.Format(CREATEEVENT, typeof(TRequest).Name));
            var context = InitRequestContext(span, requestContext);

            try
            {
                Message message = s_bus.CreateMessageFromRequest(request, context);
                
                var bus = ((IAmAnExternalBusService<Message, TTransaction>)s_bus);

                if (!bus.HasOutbox())
                    throw new InvalidOperationException("No outbox defined.");

                bus.AddToOutbox(message, context, transactionProvider);

                return message.Id;
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Adds a messages into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutbox"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns>The Id of the Message that has been deposited.</returns>
        public string[] DepositPost<TRequest>(IEnumerable<TRequest> requests, RequestContext requestContext = null, Dictionary<string, object> args = null) 
            where TRequest : class, IRequest
        {
            return DepositPost<TRequest, CommittableTransaction >(requests, null, requestContext, args); 
        }

        /// <summary>
        /// Adds a messages into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutbox"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="transactionProvider">The transaction provider to use with an outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of transaction used by the Outbox</typeparam>
        /// <returns>The Id of the Message that has been deposited.</returns>
        public string[] DepositPost<TRequest, TTransaction>(
            IEnumerable<TRequest> requests,
            IAmABoxTransactionProvider<TTransaction> transactionProvider,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null
        ) where TRequest : class, IRequest
        {
            s_logger.LogInformation("Save bulk requests request: {RequestType}", typeof(TRequest));
            
            var span = CreateSpan(string.Format(CREATEEVENT, typeof(TRequest).Name));
            var context = InitRequestContext(span, requestContext);

            try
            {
                var successfullySentMessage = new List<string>();

                foreach (var batch in SplitRequestBatchIntoTypes(requests))
                {
                    var messages = s_bus.CreateMessagesFromRequests(
                        batch.Key, batch, context, new CancellationToken()
                    ).GetAwaiter().GetResult();

                    s_logger.LogInformation(
                        "Save requests: {RequestType} {AmountOfMessages}", batch.Key, messages.Count()
                    );

                    var bus = ((IAmAnExternalBusService<Message, TTransaction>)s_bus);
                    bus.AddToOutbox(messages, context, transactionProvider);

                    successfullySentMessage.AddRange(messages.Select(m => m.Id));
                }

                return successfullySentMessage.ToArray();
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited string to <see cref="ClearOutboxAsync"/>
        /// NOTE: If you get an error about the transaction type not matching CommittableTransaction, then you need to
        /// use the specialized version of this method that takes a transaction provider.
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        public async Task<string> DepositPostAsync<TRequest>(
            TRequest request,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            return await DepositPostAsync<TRequest, CommittableTransaction>(
                request,
                null,
                requestContext,
                args,
                continueOnCapturedContext,
                cancellationToken);
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="transactionProvider">The transaction provider to use with an outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of the transaction used by the Outbox</typeparam>
        /// <returns></returns>
        public async Task<string> DepositPostAsync<TRequest, TTransaction>(
            TRequest request,
            IAmABoxTransactionProvider<TTransaction> transactionProvider,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            s_logger.LogInformation("Save request: {RequestType} {Id}", request.GetType(), request.Id);
            
            var span = CreateSpan(string.Format(CREATEEVENT, typeof(TRequest).Name));
            var context = InitRequestContext(span, requestContext);

            try
            {
                Message message = await s_bus.CreateMessageFromRequestAsync(request, context, cancellationToken);
                
                var bus = ((IAmAnExternalBusService<Message, TTransaction>)s_bus);
                
                if (!bus.HasAsyncOutbox())
                    throw new InvalidOperationException("No async outbox defined.");

                await bus.AddToOutboxAsync(message, context, transactionProvider, continueOnCapturedContext, cancellationToken);

                return message.Id;
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        public async Task<string[]> DepositPostAsync<TRequest>(
            IEnumerable<TRequest> requests,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            return await DepositPostAsync<TRequest, CommittableTransaction>(
                requests,
                null,
                requestContext,
                args,
                continueOnCapturedContext,
                cancellationToken); 
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="transactionProvider">The transaction provider used with the Outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of transaction used with the Outbox</typeparam>
        /// <returns></returns>
        public async Task<string[]> DepositPostAsync<TRequest, TTransaction>(
            IEnumerable<TRequest> requests,
            IAmABoxTransactionProvider<TTransaction> transactionProvider,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            
            var span = CreateSpan(string.Format(CREATEEVENT, typeof(TRequest).Name));
            var context = InitRequestContext(span, requestContext);

            try
            {
                var successfullySentMessage = new List<string>();

                foreach (var batch in SplitRequestBatchIntoTypes(requests))
                {
                    var messages = await s_bus.CreateMessagesFromRequests(
                        batch.Key, batch.ToArray(), context, cancellationToken
                    );

                    s_logger.LogInformation(
                        "Save requests: {RequestType} {AmountOfMessages}", batch.Key, messages.Count()
                    );
                    
                    var bus = ((IAmAnExternalBusService<Message, TTransaction>)s_bus);

                    await bus.AddToOutboxAsync(
                        messages, context, transactionProvider, continueOnCapturedContext, cancellationToken
                    );

                    successfullySentMessage.AddRange(messages.Select(m => m.Id));
                }

                return successfullySentMessage.ToArray();
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Flushes the messages in the id list from the Outbox.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPostBox"/>
        /// </summary>
        /// <param name="ids">The message ids to flush</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        public void ClearOutbox(string[] ids, RequestContext requestContext = null, Dictionary<string, object> args = null)
        {
            var span = CreateSpan(CLEAROUTBOX);
            var context = InitRequestContext(span, requestContext);
            
            try
            {
                s_bus.ClearOutbox(ids, context, args);
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Flushes any outstanding message box message to the broker.
        /// This will be run on a background task.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPostBox"/>
        /// </summary>
        /// <param name="amountToClear">The maximum number to clear.</param>
        /// <param name="minimumAge">The minimum age to clear in milliseconds.</param>
         /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>       
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        public void ClearOutbox(
            int amountToClear = 100, 
            int minimumAge = 5000, 
            RequestContext requestContext = null,
            Dictionary<string, object> args = null
            )
        {
            var span = CreateSpan(CLEAROUTBOX);
            var context = InitRequestContext(span, requestContext);

            try
            {
                s_bus.ClearOutbox(amountToClear, minimumAge, false, false, context, args);
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Flushes the message box message given by <param name="posts"/> to the broker.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPostBoxAsync"/>
        /// </summary>
        /// <param name="posts">The ids to flush</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should the callback run on a new thread?</param>
        /// <param name="cancellationToken">The token to cancel a running asynchronous operation</param>
        public async Task ClearOutboxAsync(
            IEnumerable<string> posts,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default)
        {
            var span = CreateSpan(CLEAROUTBOX);
            var context = InitRequestContext(span, requestContext);
            
            try
            {
                await s_bus.ClearOutboxAsync(posts, context, continueOnCapturedContext, args, cancellationToken);
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Flushes any outstanding message box message to the broker.
        /// This will be run on a background task.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPostBoxAsync"/>
        /// </summary>
        /// <param name="amountToClear">The maximum number to clear.</param>
        /// <param name="minimumAge">The minimum age to clear in milliseconds.</param>
        /// <param name="useBulk">Use the bulk send on the producer.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        public void ClearAsyncOutbox(
            int amountToClear = 100,
            int minimumAge = 5000,
            bool useBulk = false,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null
        )
        {
            var span = CreateSpan(CLEAROUTBOX);
            var context = InitRequestContext(span, requestContext);

            try
            {
                s_bus.ClearOutbox(amountToClear, minimumAge, true, useBulk, context, args);
            }
            finally
            {
                EndSpan(span);
            }
        }

        /// <summary>
        /// Uses the Request-Reply messaging approach to send a message to another server and block awaiting a reply.
        /// The message is placed into a message queue but not into the outbox.
        /// An ephemeral reply queue is created, and its name used to set the reply address for the response. We produce
        /// a queue per exchange, to simplify correlating send and receive.
        /// The response is directed to a registered handler.
        /// Because the operation blocks, there is a mandatory timeout
        /// </summary>
        /// <param name="request">What message do we want a reply to</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="RequestContextFactory"/> </param>
        /// <param name="timeOutInMilliseconds">The call blocks, so we must time out</param>
        /// <exception cref="NotImplementedException"></exception>
        public TResponse Call<T, TResponse>(T request, RequestContext requestContext = null, int timeOutInMilliseconds = 500)
            where T : class, ICall where TResponse : class, IResponse
        {
            if (timeOutInMilliseconds <= 0)
            {
                throw new InvalidOperationException("Timeout to a call method must have a duration greater than zero");
            }

            var subscription = _replySubscriptions.FirstOrDefault(s => s.DataType == typeof(TResponse));

            if (subscription is null)
                throw new ArgumentOutOfRangeException($"No Subscription registered fpr replies of type {typeof(T)}");

            //create a reply queue via creating a consumer - we use random identifiers as we will destroy
            var channelName = Guid.NewGuid();
            var routingKey = channelName.ToString();

            subscription.ChannelName = new ChannelName(channelName.ToString());
            subscription.RoutingKey = new RoutingKey(routingKey);

            using var responseChannel = _responseChannelFactory.CreateChannel(subscription);
            s_logger.LogInformation("Create reply queue for topic {ChannelName}", channelName);
            request.ReplyAddress.Topic = routingKey;
            request.ReplyAddress.CorrelationId = channelName.ToString();

            //we do this to create the channel on the broker, or we won't have anything to send to; we 
            //retry in case the subscription is poor. An alternative would be to extract the code from
            //the channel to create the subscription, but this does not do much on a new queue
            Retry(() => responseChannel.Purge());

            var span = CreateSpan(CLEAROUTBOX);
            var context = InitRequestContext(span, requestContext);

            try
            {
                var outMessage = s_bus.CreateMessageFromRequest(request, context);

                //We don't store the message, if we continue to fail further retry is left to the sender 
                s_logger.LogDebug("Sending request  with routingkey {ChannelName}", channelName);
                s_bus.CallViaExternalBus<T, TResponse>(outMessage);

                Message responseMessage = null;

            //now we block on the receiver to try and get the message, until timeout.
            s_logger.LogDebug("Awaiting response on {ChannelName}", channelName);
            Retry(() => responseMessage = responseChannel.Receive(timeOutInMilliseconds));

                TResponse response = default(TResponse);
                if (responseMessage.Header.MessageType != MessageType.MT_NONE)
                {
                    s_logger.LogDebug("Reply received from {ChannelName}", channelName);
                    //map to request is map to a response, but it is a request from consumer point of view. Confusing, but...
                    s_bus.CreateRequestFromMessage(responseMessage, context, out response);
                    Send(response);
                }


                s_logger.LogInformation("Deleting queue for routingkey: {ChannelName}", channelName);

                return response;
            } 
            finally
            {
                EndSpan(span);
            }
        }

            /// <summary>
        /// The external service bus is a singleton as it has app lifetime to manage an Outbox.
        /// This method clears the external service bus, so that the next attempt to use it will create a fresh one
        /// It is mainly intended for testing, to allow the external service bus to be reset between tests
        /// </summary>
        public static void ClearServiceBus()
        {
            if (s_bus != null)
            {
                lock (s_padlock)
                {
                    if (s_bus != null)
                    {
                        s_bus.Dispose();
                        s_bus = null;
                    }
                }
            }
        }

        private void AssertValidSendPipeline<T>(T command, int handlerCount) where T : class, IRequest
        {
            s_logger.LogInformation("Found {HandlerCount} pipelines for command: {Type} {Id}", handlerCount, typeof(T),
                command.Id);

            if (handlerCount > 1)
                throw new ArgumentException(
                    $"More than one handler was found for the typeof command {typeof(T)} - a command should only have one handler.");
            if (handlerCount == 0)
                throw new ArgumentException(
                    $"No command handler was found for the typeof command {typeof(T)} - a command should have exactly one handler.");
        }
        
  
        private void EndSpan(Activity span)
        {
            if (span?.Status == ActivityStatusCode.Unset)
                span.SetStatus(ActivityStatusCode.Ok);
            span?.Dispose();
        }

        private Activity CreateSpan(string activityName)
        {
            bool hasParent = Activity.Current != null;

            if (hasParent)
                return ApplicationTelemetry.ActivitySource.StartActivity(activityName, ActivityKind.Server, Activity.Current.Context);
            else
                return ApplicationTelemetry.ActivitySource.StartActivity(activityName, ActivityKind.Server);
        }
        
        private bool HandlerFactoryIsNotEitherIAmAHandlerFactorySyncOrAsync(IAmAHandlerFactory handlerFactory)
        {
            // If we do not have a subscriber registry and we do not have a handler factory 
            // then we're creating a control bus sender and we don't need them
            if (_subscriberRegistry is null && handlerFactory is null)
                return false;

            switch (handlerFactory)
            {
                case IAmAHandlerFactorySync _:
                case IAmAHandlerFactoryAsync _:
                    return false;
                default:
                    return true;
            }
        }
        
        private bool Retry(Action action)
        {
            var policy = _policyRegistry.Get<Policy>(CommandProcessor.RETRYPOLICY);
            var result = policy.ExecuteAndCapture(action);
            if (result.Outcome != OutcomeType.Successful)
            {
                if (result.FinalException != null)
                {
                    s_logger.LogError(result.FinalException, "Exception whilst trying to publish message");
                }

                return false;
            }

            return true;
        }
        
        // Create an instance of the ExternalBusService if one not already set for this app. Note that we do not support reinitialization here, so once you have
        // set a command processor for the app, you can't call init again to set them - although the properties are not read-only so overwriting is possible
        // if needed as a "get out of gaol" card.
        private static void InitExtServiceBus(IAmAnExternalBusService bus)
        {
            if (s_bus == null)
            {
                lock (s_padlock)
                {
                    s_bus ??= bus;
                }
            }
        }
        
        private RequestContext InitRequestContext(Activity span, RequestContext requestContext)
        {
            var context = requestContext ?? _requestContextFactory.Create();
            context.Span = span;
            context.Policies = _policyRegistry;
            context.FeatureSwitches = _featureSwitchRegistry;
            return context;
        }
        
        private IEnumerable<IGrouping<Type, T>> SplitRequestBatchIntoTypes<T>(IEnumerable<T> requests)
        {
            return requests.GroupBy(r => r.GetType());
        }
    }
}
