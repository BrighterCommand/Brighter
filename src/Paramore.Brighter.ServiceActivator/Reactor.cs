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
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Observability;
using Polly.CircuitBreaker;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Used when the message pump should block for I/O
    /// Will guarantee strict ordering of the messages on the queue
    /// Predictable performance as only one thread, allows you to configure the number of performers for the number of threads to use
    /// Lower throughput than async
    /// See <a href="https://www.dre.vanderbilt.edu/~schmidt/PDF/reactor-siemens.pdf">Reactor Pattern</a> for more on this approach
    /// </summary>
    public partial class Reactor : MessagePump, IAmAMessagePump
    {
        private readonly Func<Message, Type?> _mapRequestType;
        private readonly TransformPipelineBuilder _transformPipelineBuilder;

        /// <summary>
        /// Constructs a message pump 
        /// </summary>
        /// <param name="commandProcessor">Provides a way to grab a command processor correctly scoped</param>
        /// <param name="mapRequestType">Pass in a <see cref="Func{T,TResult}" />which we use to determine the type of message on the channel. For a datatype channel, always returns the same type, for cloud events uses the header type</param>
        /// <param name="messageMapperRegistry">The registry of mappers</param>
        /// <param name="messageTransformerFactory">The factory that lets us create instances of transforms</param>
        /// <param name="requestContextFactory">A factory to create instances of request synchronizationHelper, used to add synchronizationHelper to a pipeline</param>
        /// <param name="channel">The channel from which to read messages</param>
        /// <param name="tracer">What is the tracer we will use for telemetry</param>
        /// <param name="instrumentationOptions">When creating a span for <see cref="CommandProcessor"/> operations how noisy should the attributes be</param>
        public Reactor(
            IAmACommandProcessor commandProcessor,
            Func<Message, Type> mapRequestType,
            IAmAMessageMapperRegistry messageMapperRegistry, 
            IAmAMessageTransformerFactory messageTransformerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmAChannelSync channel,
            IAmABrighterTracer? tracer = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All) 
            : base(commandProcessor, requestContextFactory, tracer,  instrumentationOptions)
        {
            _mapRequestType = mapRequestType;
            _transformPipelineBuilder = new TransformPipelineBuilder(messageMapperRegistry, messageTransformerFactory, instrumentationOptions);
            Channel = channel;
        }

        /// <summary>
        /// The channel to receive messages from
        /// </summary>
        public IAmAChannelSync Channel { get; set; }
        
        /// <summary>
        /// The <see cref="MessagePumpType"/> of this message pump; indicates Reactor or Proactor
        /// </summary>
        public override MessagePumpType MessagePumpType => MessagePumpType.Reactor;

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
            var pumpSpan = Tracer?.CreateMessagePumpSpan(MessagePumpSpanOperation.Begin, Channel.RoutingKey, MessagingSystem.InternalBus, InstrumentationOptions);
            do
            {
                if (UnacceptableMessageLimitReached())
                {
                    Channel.Dispose();
                    break;
                }

                Log.ReceivingMessages(s_logger, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);

                Activity? span = null;
                Message? message = null;
                try
                {
                    message = Channel.Receive(TimeOut);
                    span = Tracer?.CreateSpan(MessagePumpSpanOperation.Receive, message, MessagingSystem.InternalBus, InstrumentationOptions);
                }
                catch (ChannelFailureException ex) when (ex.InnerException is BrokenCircuitException)
                {
                    Log.BrokenCircuitException(s_logger, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    var errorSpan = Tracer?.CreateMessagePumpExceptionSpan(ex, Channel.RoutingKey, MessagePumpSpanOperation.Receive, MessagingSystem.InternalBus, InstrumentationOptions);
                    Tracer?.EndSpan(errorSpan);
                    Task.Delay(ChannelFailureDelay).GetAwaiter().GetResult(); //-- pause pump; blocks consuming thread on empty queue; 
                    continue;
                }
                catch (ChannelFailureException ex)
                {
                    Log.ChannelFailureException(s_logger, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    var errorSpan = Tracer?.CreateMessagePumpExceptionSpan(ex, Channel.RoutingKey, MessagePumpSpanOperation.Receive, MessagingSystem.InternalBus, InstrumentationOptions);
                    Tracer?.EndSpan(errorSpan );
                    Task.Delay(ChannelFailureDelay).GetAwaiter().GetResult(); //-- pause pump; blocks consuming thread on empty queue; 
                    continue;
                }
                catch (Exception ex)
                {
                    Log.ExceptionReceivingMessages(s_logger, ex, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    var errorSpan = Tracer?.CreateMessagePumpExceptionSpan(ex, Channel.RoutingKey, MessagePumpSpanOperation.Receive, MessagingSystem.InternalBus, InstrumentationOptions);
                    Tracer?.EndSpan(errorSpan );
                }

                if (message is null)
                {
                    Channel.Dispose();
                    span?.SetStatus(ActivityStatusCode.Error, "Could not receive message. Note that should return an MT_NONE from an empty queue on timeout");
                    Tracer?.EndSpan(span);
                    throw new Exception("Could not receive message. Note that should return an MT_NONE from an empty queue on timeout");
                }

                // empty queue
                if (message.Header.MessageType == MessageType.MT_NONE)
                {
                    span?.SetStatus(ActivityStatusCode.Ok);
                    Tracer?.EndSpan(span);
                    Task.Delay(EmptyChannelDelay).GetAwaiter().GetResult();  //-- pause pump; blocks consuming thread on empty queue; 
                    continue;
                }

                // failed to parse a message from the incoming data
                if (message.Header.MessageType == MessageType.MT_UNACCEPTABLE)
                {
                    Log.FailedToParseMessage(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Failed to parse a message from the incoming message with id {message.Id} from {Channel.Name} on thread # {Environment.CurrentManagedThreadId}");
                    Tracer?.EndSpan(span);
                    IncrementUnacceptableMessageLimit();
                    AcknowledgeMessage(message);

                    continue;
                }
 
                // QUIT command
                if (message.Header.MessageType == MessageType.MT_QUIT)
                {
                    Log.QuitReceivingMessages(s_logger, Channel.Name, Environment.CurrentManagedThreadId);
                    span?.SetStatus(ActivityStatusCode.Ok);
                    Tracer?.EndSpan(span);
                    Channel.Dispose();
                    break;
                }

                // Serviceable message
                try
                {
                    RequestContext context = InitRequestContext(span, message);

                    var request = TranslateMessage(message, context);
                    
                    InvokeDispatchRequest(request, message, context);

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
                            Log.StoppingReceivingMessages(s_logger, configurationException, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                            stop = true;
                            break;
                        }

                        if (exception is DeferMessageAction)
                        {
                            defer = true;
                            continue;
                        }

                        Log.FailedToDispatchMessage(s_logger, exception, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    }

                    if (defer)
                    {
                        Log.DeferringMessage(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
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
                    Log.StoppingReceivingMessages2(s_logger, configurationException, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    RejectMessage(message);
                    span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Stopping receiving of messages from {Channel.Name} on thread # {Environment.CurrentManagedThreadId}");
                    Channel.Dispose();
                    break;
                }
                catch (DeferMessageAction)
                {
                    Log.DeferringMessage2(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    
                    span?.SetStatus(ActivityStatusCode.Error, $"Deferring message {message.Id} for later action");
                    
                    if (RequeueMessage(message)) continue;
                }
                catch (MessageMappingException messageMappingException)
                {
                    Log.FailedToMapMessage(s_logger, messageMappingException, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);

                    IncrementUnacceptableMessageLimit();
                    
                    span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Failed to map message {message.Id} from {Channel.Name} with {Channel.RoutingKey} on thread # {Thread.CurrentThread.ManagedThreadId}");
                }
                catch (Exception e)
                {
                    Log.FailedToDispatchMessage2(s_logger, e, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);

                    span?.SetStatus(ActivityStatusCode.Error,$"MessagePump: Failed to dispatch message '{message.Id}' from {Channel.Name} with {Channel.RoutingKey} on thread # {Environment.CurrentManagedThreadId}");
                }
                finally
                {
                    Tracer?.EndSpan(span);
                }

                AcknowledgeMessage(message);

            } while (true);

            Log.FinishedRunningMessageLoop(s_logger, Channel.Name, Channel.RoutingKey, Thread.CurrentThread.ManagedThreadId);
            Tracer?.EndSpan(pumpSpan);

        }

        private void AcknowledgeMessage(Message message)
        {
            Log.AcknowledgeMessage(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);

            Channel.Acknowledge(message);
        }
        
        private void DispatchRequest<TRequest>(MessageHeader messageHeader, TRequest request, RequestContext requestContext) where TRequest : class, IRequest
        {
            Log.DispatchingMessage(s_logger, request.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);
            requestContext.Span?.AddEvent(new ActivityEvent("Dispatch Message"));

            var messageType = messageHeader.MessageType;

            ValidateMessageType(messageType, request);

            switch (messageType)
            {
                case MessageType.MT_COMMAND:
                {
                    CommandProcessor.Send(request, requestContext);
                    break;
                }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                {
                    CommandProcessor.Publish(request, requestContext);
                    break;
                }
            }
        }

        private RequestContext InitRequestContext(Activity? span, Message message)
        {
            var context = RequestContextFactory.Create();
            context.Span = span;
            context.OriginatingMessage = message;
            context.Bag.AddOrUpdate("ChannelName", Channel.Name, (_, _) => Channel.Name);
            context.Bag.AddOrUpdate("RequestStart", DateTime.UtcNow, (_, _) => DateTime.UtcNow);
            return context;
        }

        private bool RejectMessage(Message message)
        {
            Log.RejectingMessage(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
            IncrementUnacceptableMessageLimit();

            return Channel.Reject(message);
        }
        
        private void InvokeDispatchRequest(IRequest request, Message message, RequestContext context)
        {
            // NOTE: DispatchRequest<TRequest> is a generic method constrained to TRequest : class, IRequest, but at runtime
            // we only have an IRequest reference due to the dynamic type lookup. To call the generic method with the actual
            // runtime type (e.g., MyEvent), we need to use reflection to construct and invoke the generic method with the correct type.

            try
            {
                MethodInfo dispatchMethod = MakeDispatchMethod(request);

                dispatchMethod.Invoke(this, [message.Header, request, context]);
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException ?? tie; // Unwrap the inner exception if it exists
            }
        }

        private MethodInfo MakeDispatchMethod(IRequest request)
        {
            var requestType = request.GetType();
            MethodInfo? dispatchMethod;
            if (DispatchMethodCache.TryGetValue(request.GetType(), out var cachedDispatchMethod))
            {
                dispatchMethod = cachedDispatchMethod;
            }
            else
            {
                dispatchMethod = typeof(Reactor)
                    .GetMethod(nameof(DispatchRequest), BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.MakeGenericMethod(requestType);
                DispatchMethodCache[requestType] = dispatchMethod!;
            }

            if (dispatchMethod is null)
                throw new InvalidOperationException($"Could not find DispatchRequest method for type {requestType.FullName}");
            return dispatchMethod;
        }

        private object? MakeUnwrapPipeline(Type requestType)
        {
            MethodInfo typedPipelineFactory;
            if (UnWrapPipelineFactoryCache.TryGetValue(requestType, out var cachedPipelineFactory))
            {
                typedPipelineFactory = cachedPipelineFactory;
            }
            else
            {
                // Get the generic method definition
                var pipelineFactory = typeof(TransformPipelineBuilder).GetMethod(nameof(TransformPipelineBuilder.BuildUnwrapPipeline), Type.EmptyTypes);

                // Make the generic method with the runtime type
                typedPipelineFactory = pipelineFactory!.MakeGenericMethod(requestType);
                UnWrapPipelineFactoryCache[requestType] = typedPipelineFactory;
            }

            // Invoke the method to get the pipeline instance
            var pipeline = typedPipelineFactory.Invoke(_transformPipelineBuilder, null);
            return pipeline;
        }

        private bool RequeueMessage(Message message)
        {
            message.Header.UpdateHandledCount();

            if (DiscardRequeuedMessagesEnabled())
            {
                if (message.HandledCountReached(RequeueCount))
                {
                    var originalMessageId = message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out object? value) ? value.ToString() : null;

                    Log.DroppingMessage(s_logger, RequeueCount, message.Id, string.IsNullOrEmpty(originalMessageId)
                            ? string.Empty
                            : $" (original message id {originalMessageId})", Channel.Name, Channel.RoutingKey, Thread.CurrentThread.ManagedThreadId);

                    return RejectMessage(message);
                }
            }

            Log.RequeueingMessage(s_logger, message.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name, Channel.RoutingKey);

            return Channel.Requeue(message, RequeueDelay);
        }
        
        private IRequest TranslateMessage(Message message, RequestContext requestContext)
        {
            Log.TranslateMessage(s_logger, message.Id, Thread.CurrentThread.ManagedThreadId);
            requestContext.Span?.AddEvent(new ActivityEvent("Translate Message"));

            IRequest request;

            var requestType = _mapRequestType(message);
            if (requestType == null)
                throw new MessageMappingException($"Failed to find request type for message {message.Id} ", 
                    new ArgumentNullException(nameof(requestType), "The request type cannot be null."));
            
            try
            {
                object? pipeline = MakeUnwrapPipeline(requestType);

                // Call Unwrap on the pipeline
                var unwrapMethod = pipeline!.GetType().GetMethod("Unwrap");
                request = (IRequest)unwrapMethod!.Invoke(pipeline, [message, requestContext])!;
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new MessageMappingException($"Failed to map message {message.Id} of {requestType.FullName} using transform pipeline ", exception);
            }

            return request;
        }

        private bool UnacceptableMessageLimitReached()
        {
            if (UnacceptableMessageLimit == 0) return false;

            if (UnacceptableMessageCount >= UnacceptableMessageLimit)
            {
                Log.UnacceptableMessageLimitReached(s_logger, UnacceptableMessageLimit, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                
                return true;
            }
            return false;
        }
        
        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "MessagePump: Receiving messages from channel {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void ReceivingMessages(ILogger logger, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Warning, "MessagePump: BrokenCircuitException messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void BrokenCircuitException(ILogger logger, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Warning, "MessagePump: ChannelFailureException messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void ChannelFailureException(ILogger logger, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Error, "MessagePump: Exception receiving messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void ExceptionReceivingMessages(ILogger logger, Exception ex, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Warning, "MessagePump: Failed to parse a message from the incoming message with id {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void FailedToParseMessage(ILogger logger, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Information, "MessagePump: Quit receiving messages from {ChannelName} on thread #{ManagementThreadId}")]
            internal static partial void QuitReceivingMessages(ILogger logger, string? channelName, int managementThreadId);
            
            [LoggerMessage(LogLevel.Critical, "MessagePump: Stopping receiving of messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void StoppingReceivingMessages(ILogger logger, Exception ex, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Error, "MessagePump: Failed to dispatch message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void FailedToDispatchMessage(ILogger logger, Exception ex, string id, string? channelName, string routingKey, int managementThreadId);

            [LoggerMessage(LogLevel.Debug, "MessagePump: Deferring message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void DeferringMessage(ILogger logger, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Critical, "MessagePump: Stopping receiving of messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void StoppingReceivingMessages2(ILogger logger, ConfigurationException configurationException, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Debug, "MessagePump: Deferring message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void DeferringMessage2(ILogger logger, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Warning, "MessagePump: Failed to map message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void FailedToMapMessage(ILogger logger, Exception ex, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Error, "MessagePump: Failed to dispatch message '{Id}' from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void FailedToDispatchMessage2(ILogger logger, Exception e, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Debug, "MessagePump: Acknowledge message {Id} read from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void AcknowledgeMessage(ILogger logger, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Debug, "MessagePump: Dispatching message {Id} from {ChannelName} on thread # {ManagementThreadId}")]
            internal static partial void DispatchingMessage(ILogger logger, string id, int managementThreadId, string? channelName);
            
            [LoggerMessage(LogLevel.Warning, "MessagePump: Rejecting message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void RejectingMessage(ILogger logger, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Error, "MessagePump: Have tried {RequeueCount} times to handle this message {Id}{OriginalMessageId} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}, dropping message.")]
            internal static partial void DroppingMessage(ILogger logger, int requeueCount, string id, string originalMessageId, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Debug, "MessagePump: Re-queueing message {Id} from {ManagementThreadId} on thread # {ChannelName} with {RoutingKey}")]
            internal static partial void RequeueingMessage(ILogger logger, string id, int managementThreadId, string? channelName, string routingKey);
            
            [LoggerMessage(LogLevel.Debug, "MessagePump: Translate message {Id} on thread # {ManagementThreadId}")]
            internal static partial void TranslateMessage(ILogger logger, string id, int managementThreadId);
            
            [LoggerMessage(LogLevel.Critical, "MessagePump: Unacceptable message limit of {UnacceptableMessageLimit} reached, stopping reading messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void UnacceptableMessageLimitReached(ILogger logger, int unacceptableMessageLimit, string? channelName, string routingKey, int managementThreadId);

            [LoggerMessage(LogLevel.Information, "MessagePump0: Finished running message loop, no longer receiving messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void FinishedRunningMessageLoop(ILogger logger, string? channelName, string routingKey, int managementThreadId);
        }
    }
}

