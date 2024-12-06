#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Polly.CircuitBreaker;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// The message pump is the heart of a consumer. It runs a loop that performs the following:
    ///  - Gets a message from a queue/stream
    ///  - Translates the message to the local type system
    ///  - Dispatches the message to waiting handlers
    /// The message pump is a classic event loop and is intended to be run on a single-thread
    /// The event loop is terminated when reading a MT_QUIT message on the channel
    /// The event loop blocks on the Channel Listen call, though it will timeout
    /// The event loop calls user code synchronously. You can post again for further decoupled invocation, but of course the likelihood is we are supporting decoupled invocation elsewhere
    /// This is why you should spin up a thread for your message pump: to avoid blocking your main control path while you listen for a message and process it
    /// It is also why throughput on a queue needs multiple performers, each with their own message pump
    /// Retry and circuit breaker should be provided by exception policy using an attribute on the handler
    /// Timeout on the handler should be provided by timeout policy using an attribute on the handler 
    /// </summary>
    public abstract class MessagePump<TRequest> : IAmAMessagePump where TRequest : class, IRequest
    {
        internal static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MessagePump<TRequest>>();

        protected readonly IAmACommandProcessorProvider CommandProcessorProvider;
        private readonly IAmARequestContextFactory _requestContextFactory;
        private readonly IAmABrighterTracer? _tracer;
        private readonly InstrumentationOptions _instrumentationOptions;
        private int _unacceptableMessageCount;

        /// <summary>
        /// Constructs a message pump. The message pump is the heart of a consumer. It runs a loop that performs the following:
        ///  - Gets a message from a queue/stream
        ///  - Translates the message to the local type system
        ///  - Dispatches the message to waiting handlers
        ///  The message pump is a classic event loop and is intended to be run on a single-thread 
        /// </summary>
        /// <param name="commandProcessorProvider">Provides a correctly scoped command processor </param>
        /// <param name="requestContextFactory">Provides a request context</param>
        /// <param name="tracer">What is the <see cref="BrighterTracer"/> we will use for telemetry</param>
        /// <param name="channel"></param>
        /// <param name="instrumentationOptions">When creating a span for <see cref="CommandProcessor"/> operations how noisy should the attributes be</param>
        protected MessagePump(
            IAmACommandProcessorProvider commandProcessorProvider, 
            IAmARequestContextFactory requestContextFactory,
            IAmABrighterTracer? tracer,
            IAmAChannel channel,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
            CommandProcessorProvider = commandProcessorProvider;
            _requestContextFactory = requestContextFactory;
            _tracer = tracer;
            _instrumentationOptions = instrumentationOptions;
            Channel = channel;
        }

        /// <summary>
        /// How long to wait for a message before timing out
        /// </summary>
        public TimeSpan TimeOut { get; set; }

        /// <summary>
        /// How many times to requeue a message before discarding it
        /// </summary>
        public int RequeueCount { get; set; }

        /// <summary>
        /// How long to wait before requeuing a message
        /// </summary>
        public TimeSpan RequeueDelay { get; set; }

        /// <summary>
        /// The number of unacceptable messages to receive before stopping the message pump
        /// </summary>
        public int UnacceptableMessageLimit { get; set; }

        /// <summary>
        /// The channel to receive messages from
        /// </summary>
        public IAmAChannel Channel { get; set; }
        
        /// <summary>
        /// The delay to wait when the channel is empty
        /// </summary>
        public int EmptyChannelDelay { get; set; }
        
        /// <summary>
        /// The delay to wait when the channel has failed
        /// </summary>
        public int ChannelFailureDelay { get; set; }

        /// <summary>
        /// Runs the message pump, performing the following:
        /// - Gets a message from a queue/stream
        /// - Translates the message to the local type system
        /// - Dispatches the message to waiting handlers
        /// - Handles any exceptions that occur during the dispatch and tries to keep the pump alive  
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Run()
        {
            var pumpSpan = _tracer?.CreateMessagePumpSpan(MessagePumpSpanOperation.Begin, Channel.RoutingKey, MessagingSystem.InternalBus, _instrumentationOptions);
            do
            {
                if (UnacceptableMessageLimitReached())
                {
                    Channel.Dispose();
                    break;
                }

                s_logger.LogDebug("MessagePump: Receiving messages from channel {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);

                Activity? span = null;
                Message? message = null;
                try
                {
                    message = Channel.Receive(TimeOut);
                    span = _tracer?.CreateSpan(MessagePumpSpanOperation.Receive, message, MessagingSystem.InternalBus, _instrumentationOptions);
                }
                catch (ChannelFailureException ex) when (ex.InnerException is BrokenCircuitException)
                {
                    s_logger.LogWarning("MessagePump: BrokenCircuitException messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                     var errorSpan = _tracer?.CreateMessagePumpExceptionSpan(ex, Channel.RoutingKey, MessagePumpSpanOperation.Receive, MessagingSystem.InternalBus, _instrumentationOptions);
                    _tracer?.EndSpan(errorSpan);
                    Task.Delay(ChannelFailureDelay).GetAwaiter().GetResult(); //-- pause pump; blocks consuming thread on empty queue; in async code continuation runs on BrighterSynchronizationContext
                    continue;
                }
                catch (ChannelFailureException ex)
                {
                    s_logger.LogWarning("MessagePump: ChannelFailureException messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                     var errorSpan = _tracer?.CreateMessagePumpExceptionSpan(ex, Channel.RoutingKey, MessagePumpSpanOperation.Receive, MessagingSystem.InternalBus, _instrumentationOptions);
                    _tracer?.EndSpan(errorSpan );
                    Task.Delay(ChannelFailureDelay).GetAwaiter().GetResult(); //-- pause pump; blocks consuming thread on empty queue; in async code continuation runs on BrighterSynchronizationContext
                    continue;
                }
                catch (Exception ex)
                {
                    s_logger.LogError(ex, "MessagePump: Exception receiving messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    var errorSpan = _tracer?.CreateMessagePumpExceptionSpan(ex, Channel.RoutingKey, MessagePumpSpanOperation.Receive, MessagingSystem.InternalBus, _instrumentationOptions);
                    _tracer?.EndSpan(errorSpan );
                }

                if (message is null)
                {
                     Channel.Dispose();
                     span?.SetStatus(ActivityStatusCode.Error, "Could not receive message. Note that should return an MT_NONE from an empty queue on timeout");
                     _tracer?.EndSpan(span);
                     throw new Exception("Could not receive message. Note that should return an MT_NONE from an empty queue on timeout");
                }

                // empty queue
                if (message.Header.MessageType == MessageType.MT_NONE)
                {
                    span?.SetStatus(ActivityStatusCode.Ok);
                    _tracer?.EndSpan(span);
                    Task.Delay(EmptyChannelDelay).GetAwaiter().GetResult();  //-- pause pump; blocks consuming thread on empty queue; in async code continuation runs on BrighterSynchronizationContext
                    continue;
                }

                // failed to parse a message from the incoming data
                if (message.Header.MessageType == MessageType.MT_UNACCEPTABLE)
                {
                    s_logger.LogWarning("MessagePump: Failed to parse a message from the incoming message with id {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Failed to parse a message from the incoming message with id {message.Id} from {Channel.Name} on thread # {Environment.CurrentManagedThreadId}");
                    _tracer?.EndSpan(span);
                    IncrementUnacceptableMessageLimit();
                    AcknowledgeMessage(message);

                    continue;
                }
 
                // QUIT command
                if (message.Header.MessageType == MessageType.MT_QUIT)
                {
                    s_logger.LogInformation("MessagePump: Quit receiving messages from {ChannelName} on thread #{ManagementThreadId}", Channel.Name, Environment.CurrentManagedThreadId);
                    span?.SetStatus(ActivityStatusCode.Ok);
                    _tracer?.EndSpan(span);
                    Channel.Dispose();
                    break;
                }

                // Serviceable message
                try
                {
                    RequestContext context = InitRequestContext(span, message);

                    var request = TranslateMessage(message, context);
                    
                    CommandProcessorProvider.CreateScope();
                    
                    DispatchRequest(message.Header, request, context);

                    span?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (AggregateException aggregateException)
                {
                    var stop = false;
                    var defer = false;
  
                    foreach (var exception in aggregateException.InnerExceptions)
                    {
                        if (exception is ConfigurationException configurationException)
                        {
                            s_logger.LogCritical(configurationException, "MessagePump: Stopping receiving of messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                            stop = true;
                            break;
                        }

                        if (exception is DeferMessageAction)
                        {
                            defer = true;
                            continue;
                        }

                        s_logger.LogError(exception, "MessagePump: Failed to dispatch message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    }

                    if (defer)
                    {
                        s_logger.LogDebug("MessagePump: Deferring message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                        span?.SetStatus(ActivityStatusCode.Error, $"Deferring message {message.Id} for later action");
                        if (RequeueMessage(message))
                            continue;
                    }

                    if (stop)
                    {
                        RejectMessage(message);
                        span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Stopping receiving of messages from {Channel.Name} with {Channel.RoutingKey} on thread # {Environment.CurrentManagedThreadId}");
                        Channel.Dispose();
                        break;
                    }

                    span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Failed to dispatch message {message.Id} from {Channel.Name} with {Channel.RoutingKey}  on thread # {Environment.CurrentManagedThreadId}");
                }
                catch (ConfigurationException configurationException)
                {
                    s_logger.LogCritical(configurationException,"MessagePump: Stopping receiving of messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    RejectMessage(message);
                    span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Stopping receiving of messages from {Channel.Name} on thread # {Environment.CurrentManagedThreadId}");
                    Channel.Dispose();
                    break;
                }
                catch (DeferMessageAction)
                {
                    s_logger.LogDebug("MessagePump: Deferring message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    
                    span?.SetStatus(ActivityStatusCode.Error, $"Deferring message {message.Id} for later action");
                    
                    if (RequeueMessage(message)) continue;
                }
                catch (MessageMappingException messageMappingException)
                {
                    s_logger.LogWarning(messageMappingException, "MessagePump: Failed to map message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);

                    IncrementUnacceptableMessageLimit();
                    
                    span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Failed to map message {message.Id} from {Channel.Name} with {Channel.RoutingKey} on thread # {Thread.CurrentThread.ManagedThreadId}");
                }
                catch (Exception e)
                {
                    s_logger.LogError(e,
                        "MessagePump: Failed to dispatch message '{Id}' from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}",
                        message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);

                    span?.SetStatus(ActivityStatusCode.Error,$"MessagePump: Failed to dispatch message '{message.Id}' from {Channel.Name} with {Channel.RoutingKey} on thread # {Environment.CurrentManagedThreadId}");
                }
                finally
                {
                    _tracer?.EndSpan(span);
                    CommandProcessorProvider.ReleaseScope();
                }

                AcknowledgeMessage(message);

            } while (true);

            s_logger.LogInformation(
                "MessagePump0: Finished running message loop, no longer receiving messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}",
                Channel.Name, Channel.RoutingKey, Thread.CurrentThread.ManagedThreadId);
            _tracer?.EndSpan(pumpSpan);

        }
 
        private void AcknowledgeMessage(Message message)
        {
            s_logger.LogDebug(
                "MessagePump: Acknowledge message {Id} read from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}",
                message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);

            Channel.Acknowledge(message);
        }
        
        private bool DiscardRequeuedMessagesEnabled()
        {
            return RequeueCount != -1;
        }

        // Implemented in a derived class to dispatch to the relevant type of pipeline via the command processor
        // i..e an async pipeline uses SendAsync/PublishAsync and a blocking pipeline uses Send/Publish
        protected abstract void DispatchRequest(MessageHeader messageHeader, TRequest request, RequestContext context);

        private void IncrementUnacceptableMessageLimit()
        {
            _unacceptableMessageCount++;
        }
        
        private RequestContext InitRequestContext(Activity? span, Message message)
        {
            var context = _requestContextFactory.Create();
            context.Span = span;
            context.OriginatingMessage = message;
            context.Bag.AddOrUpdate("ChannelName", Channel.Name, (_, _) => Channel.Name);
            context.Bag.AddOrUpdate("RequestStart", DateTime.UtcNow, (_, _) => DateTime.UtcNow);
            return context;
        }

        private void RejectMessage(Message message)
        {
            s_logger.LogWarning("MessagePump: Rejecting message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}", message.Id, Channel.Name, Channel.RoutingKey, Thread.CurrentThread.ManagedThreadId);
            IncrementUnacceptableMessageLimit();

            Channel.Reject(message);
        }

        /// <summary>
        /// Requeue Message
        /// </summary>
        /// <param name="message">Message to be Requeued</param>
        /// <returns>Returns True if the message should be acked, false if the channel has handled it</returns>
        private bool RequeueMessage(Message message)
        {
            message.Header.UpdateHandledCount();

            if (DiscardRequeuedMessagesEnabled())
            {
                if (message.HandledCountReached(RequeueCount))
                {
                    var originalMessageId = message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out object? value) ? value.ToString() : null;

                    s_logger.LogError(
                        "MessagePump: Have tried {RequeueCount} times to handle this message {Id}{OriginalMessageId} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}, dropping message.",
                        RequeueCount,
                        message.Id,
                        string.IsNullOrEmpty(originalMessageId)
                            ? string.Empty
                            : $" (original message id {originalMessageId})",
                        Channel.Name,
                        Channel.RoutingKey,
                        Thread.CurrentThread.ManagedThreadId);

                    RejectMessage(message);
                    return false;
                }
            }

            s_logger.LogDebug(
                "MessagePump: Re-queueing message {Id} from {ManagementThreadId} on thread # {ChannelName} with {RoutingKey}", message.Id,
                Channel.Name, Channel.RoutingKey, Thread.CurrentThread.ManagedThreadId);

            return Channel.Requeue(message, RequeueDelay);
        }

        protected abstract TRequest TranslateMessage(Message message, RequestContext requestContext);

        private bool UnacceptableMessageLimitReached()
        {
            if (UnacceptableMessageLimit == 0) return false;

            if (_unacceptableMessageCount >= UnacceptableMessageLimit)
            {
                s_logger.LogCritical(
                    "MessagePump: Unacceptable message limit of {UnacceptableMessageLimit} reached, stopping reading messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}",
                    UnacceptableMessageLimit,
                    Channel.Name,
                    Channel.RoutingKey,
                    Environment.CurrentManagedThreadId
                );
                
                return true;
            }
            return false;
        }

        protected void ValidateMessageType(MessageType messageType, TRequest request)
        {
            if (messageType == MessageType.MT_COMMAND && request is IEvent)
            {
                throw new ConfigurationException(string.Format("Message {0} mismatch. Message type is '{1}' yet mapper produced message of type IEvent", request.Id,
                    MessageType.MT_COMMAND));
            }

            if (messageType == MessageType.MT_EVENT && request is ICommand)
            {
                throw new ConfigurationException(string.Format("Message {0} mismatch. Message type is '{1}' yet mapper produced message of type ICommand", request.Id,
                    MessageType.MT_EVENT));
            }
        }
   }
}
