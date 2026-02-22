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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;
using Polly.CircuitBreaker;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Used when we don't want to block for I/O, but queue on a completion port and be notified when done
    /// <remarks>See <a href ="https://www.dre.vanderbilt.edu/~schmidt/PDF/Proactor.pdf">Proactor Pattern</a></remarks> 
    /// </summary>
    public partial class Proactor : MessagePump, IAmAMessagePump
    {
        private readonly Func<Message, Type> _mapRequestType;
        private readonly TransformPipelineBuilderAsync _transformPipelineBuilder;


        /// <summary>
        /// Constructs a message pump 
        /// </summary>
        /// <param name="commandProcessor">Provides a way to grab a command processor correctly scoped</param>
        /// <param name="mapRequestType">Pass in a <see cref="Func{T,TResult}" />which we use to determine the type of message on the channel. For a datatype channel, always returns the same type, for cloud events uses the header type</param>
        /// <param name="messageMapperRegistry">The registry of mappers</param>
        /// <param name="messageTransformerFactory">The factory that lets us create instances of transforms</param>
        /// <param name="requestContextFactory">A factory to create instances of request synchronizationHelper, used to add synchronizationHelper to a pipeline</param>
        /// <param name="channel">The channel to read messages from</param>
        /// <param name="tracer">What is the tracer we will use for telemetry</param>
        /// <param name="instrumentationOptions">When creating a span for <see cref="CommandProcessor"/> operations how noisy should the attributes be</param>
        /// <param name="timeProvider">The <see cref="TimeProvider"/> used by the pump. Defaults to TimeProvider.System, intended for testing</param>
        public Proactor(
            IAmACommandProcessor commandProcessor,
            Func<Message, Type> mapRequestType,
            IAmAMessageMapperRegistryAsync messageMapperRegistry, 
            IAmAMessageTransformerFactoryAsync messageTransformerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmAChannelAsync channel,
            IAmABrighterTracer? tracer = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All,
            TimeProvider? timeProvider = null) 
            : base(commandProcessor, requestContextFactory, tracer, instrumentationOptions, timeProvider)
        {
            _mapRequestType = mapRequestType;
            _transformPipelineBuilder = new TransformPipelineBuilderAsync(messageMapperRegistry, messageTransformerFactory, instrumentationOptions);
            Channel = channel;
        }

        /// <summary>
        /// The channel to receive messages from
        /// </summary>
        public IAmAChannelAsync Channel { get; set; }
        
        /// <summary>
        /// The <see cref="MessagePumpType"/> of this message pump; indicates Reactor or Proactor
        /// </summary>
        public override MessagePumpType MessagePumpType => MessagePumpType.Proactor;
        
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
            //NOTE: Don't make this a method body, as opposed to an expression, unless you want it to
            //break deep in AsyncTaskMethodBuilder for some hard to explain reasons
            BrighterAsyncContext.Run(() => EventLoop());
        }

        private async Task Acknowledge(Message message)
        {
            Log.AcknowledgeMessage(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);

            await Channel.AcknowledgeAsync(message);
        }
        
        private async Task DispatchRequest<TRequest>(MessageHeader messageHeader, TRequest request, RequestContext requestContext) where TRequest : class, IRequest
        {
            Log.DispatchingMessage(s_logger, request.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);
            requestContext.Span?.AddEvent(new ActivityEvent("Dispatch Message"));

            var messageType = messageHeader.MessageType;
            
            ValidateMessageType(messageType, request);

            switch (messageType)
            {
                case MessageType.MT_COMMAND:
                {
                    await CommandProcessor
                        .SendAsync(request,requestContext, continueOnCapturedContext: true, default);
                    break;
                }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                {
                    await CommandProcessor
                        .PublishAsync(request, requestContext, continueOnCapturedContext: true, default);
                    break;
                }
            }
        }

        private async Task EventLoop()
        {
            var pumpSpan = Tracer?.CreateMessagePumpSpan(MessagePumpSpanOperation.Begin, Channel.RoutingKey, MessagingSystem.InternalBus, InstrumentationOptions);

            do
            {
                if (UnacceptableMessageLimitReached())
                {
                    Channel.Dispose();
                    Status = MessagePumpStatus.MP_LIMIT_EXCEEDED;
                    break;
                }

                Log.ReceivingMessagesFromChannel(s_logger, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);

                Activity? span = null;
                Message? message = null;
                try
                {
                    message =  await Channel.ReceiveAsync(TimeOut);
                    span = Tracer?.CreateSpan(MessagePumpSpanOperation.Receive, message, MessagingSystem.InternalBus, InstrumentationOptions);
                }
                catch (ChannelFailureException ex) when (ex.InnerException is BrokenCircuitException)
                {
                    Log.BrokenCircuitExceptionMessages(s_logger, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    var errorSpan = Tracer?.CreateMessagePumpExceptionSpan(ex, Channel.RoutingKey, MessagePumpSpanOperation.Receive, MessagingSystem.InternalBus, InstrumentationOptions);
                    Tracer?.EndSpan(errorSpan);
                     await Task.Delay(ChannelFailureDelay); 
                    continue;
                }
                catch (ChannelFailureException ex)
                {
                    Log.ChannelFailureExceptionMessages(s_logger, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    var errorSpan = Tracer?.CreateMessagePumpExceptionSpan(ex, Channel.RoutingKey, MessagePumpSpanOperation.Receive, MessagingSystem.InternalBus, InstrumentationOptions);
                    Tracer?.EndSpan(errorSpan );
                    await Task.Delay(ChannelFailureDelay); 
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
                    Status = MessagePumpStatus.MP_ERROR;
                    throw new Exception("Could not receive message. Note that should return an MT_NONE from an empty queue on timeout");
                }

                // empty queue
                if (message.Header.MessageType == MessageType.MT_NONE)
                {
                    span?.SetStatus(ActivityStatusCode.Ok);
                    Tracer?.EndSpan(span);
                    await Task.Delay(EmptyChannelDelay); 
                    continue;
                }

                // failed to parse a message from the incoming data
                if (message.Header.MessageType == MessageType.MT_UNACCEPTABLE)
                {
                    Log.FailedToParseMessage(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    var description = $"MessagePump: Failed to parse a message from the incoming message with id {message.Id} from {Channel.Name} on thread # {Environment.CurrentManagedThreadId}";
                    span?.SetStatus(ActivityStatusCode.Error, description);    
                    Tracer?.EndSpan(span);
                    IncrementUnacceptableMessageCount();
                    await RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, description));

                    continue;
                }
 
                // QUIT command
                if (message.Header.MessageType == MessageType.MT_QUIT)
                {
                    Log.QuitReceivingMessages(s_logger, Channel.Name, Environment.CurrentManagedThreadId);
                    span?.SetStatus(ActivityStatusCode.Ok);
                    Tracer?.EndSpan(span);
                    Channel.Dispose();
                    Status = MessagePumpStatus.MP_STOPPED;
                    break;
                }

                // Serviceable message
                try
                {
                    RequestContext context = InitRequestContext(span, message);

                    var request = await TranslateMessage(message, context);
                    
                    await InvokeDispatchRequest(request, message, context);

                    span?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (AggregateException aggregateException)
                {
                    var stop = false;
                    var defer = false;
                    DontAckAction? dontAck = null;
                    var reject = false;
                    var invalidMessage = false;
                    string? rejectReason = null;

                    foreach (var exception in aggregateException.InnerExceptions)
                    {
                        if (exception is ConfigurationException configurationException)
                        {
                            Log.StoppingReceivingMessages(s_logger, configurationException, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                            stop = true;
                            Status = MessagePumpStatus.MP_ERROR;
                            break;
                        }

                        if (exception is DeferMessageAction)
                        {
                            defer = true;
                            continue;
                        }

                        if (exception is DontAckAction dontAckAction)
                        {
                            dontAck = dontAckAction;
                            continue;
                        }

                        if (exception is RejectMessageAction rejectMessageAction)
                        {
                            reject = true;
                            rejectReason = rejectMessageAction.Message;
                            continue;
                        }

                        if (exception is InvalidMessageAction invalidMessageAction)
                        {
                            invalidMessage = true;
                            rejectReason = invalidMessageAction.Message;
                            continue;
                        }

                        Log.FailedToDispatchMessage(s_logger, exception, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    }

                    if (defer)
                    {
                        Log.DeferringMessage(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                        span?.SetStatus(ActivityStatusCode.Error, $"Deferring message {message.Id} for later action");
                        if (await RequeueMessage(message))
                            continue;
                    }

                    if (dontAck != null)
                    {
                        Log.NotAcknowledgingMessage(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                        if (dontAck.InnerException != null)
                            Log.DontAckActionInnerException(s_logger, dontAck.InnerException, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                        span?.SetStatus(ActivityStatusCode.Error, $"Don't Ack Thrown. Not acknowledging message {message.Id}");
                        await Channel.NackAsync(message);
                        IncrementUnacceptableMessageCount();
                        await Task.Delay(DontAckDelay);
                        continue;
                    }

                    if (reject)
                    {
                        span?.SetStatus(ActivityStatusCode.Error, $"Rejecting message {message.Id}");
                        IncrementUnacceptableMessageCount();
                        await RejectMessage(message, new MessageRejectionReason(RejectionReason.DeliveryError, rejectReason));
                        continue;
                    }

                    if (invalidMessage)
                    {
                        span?.SetStatus(ActivityStatusCode.Error, $"Invalid message {message.Id}");
                        IncrementUnacceptableMessageCount();
                        await RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, rejectReason));
                        continue;
                    }

                    if (stop)
                    {
                        await RejectMessage(message, new MessageRejectionReason(RejectionReason.DeliveryError, $"Not processed due to configuration exception: {rejectReason}"));
                        span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Stopping receiving of messages from {Channel.Name} with {Channel.RoutingKey} on thread # {Environment.CurrentManagedThreadId}");
                        Channel.Dispose();
                        break;
                    }

                    span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Failed to dispatch message {message.Id} from {Channel.Name} with {Channel.RoutingKey}  on thread # {Environment.CurrentManagedThreadId}");
                }
                catch (ConfigurationException configurationException)
                {
                    Log.StoppingReceivingMessages2(s_logger, configurationException, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    await RejectMessage(message, new MessageRejectionReason(RejectionReason.DeliveryError,$"Not processed due to configuration exception: {configurationException.Message}"));
                    span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Stopping receiving of messages from {Channel.Name} on thread # {Environment.CurrentManagedThreadId}");
                    Channel.Dispose();
                    Status = MessagePumpStatus.MP_ERROR;
                    break;
                }
                catch (DeferMessageAction)
                {
                    Log.DeferringMessage2(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);

                    span?.SetStatus(ActivityStatusCode.Error, $"Deferring message {message.Id} for later action");

                    if (await RequeueMessage(message)) continue;
                }
                catch (DontAckAction dontAckAction)
                {
                    Log.NotAcknowledgingMessage(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    if (dontAckAction.InnerException != null)
                        Log.DontAckActionInnerException(s_logger, dontAckAction.InnerException, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    span?.SetStatus(ActivityStatusCode.Error, $"Don't Ack Thrown. Not acknowledging message {message.Id}");
                    await Channel.NackAsync(message);
                    IncrementUnacceptableMessageCount();
                    await Task.Delay(DontAckDelay);
                    continue;
                }
                catch (RejectMessageAction rejectMessageAction)
                {
                    span?.SetStatus(ActivityStatusCode.Error, $"Rejecting message {message.Id}");
                    IncrementUnacceptableMessageCount();
                    await RejectMessage(message, new MessageRejectionReason(RejectionReason.DeliveryError, rejectMessageAction.Message));

                    continue;
                }
                catch (InvalidMessageAction invalidMessageAction)
                {
                    span?.SetStatus(ActivityStatusCode.Error, $"Invalid message {message.Id}");
                    IncrementUnacceptableMessageCount();
                    await RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, invalidMessageAction.Message));
                    continue;
                }
                catch (MessageMappingException messageMappingException)
                {
                    Log.FailedToMapMessage(s_logger, messageMappingException, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    IncrementUnacceptableMessageCount();
                    span?.SetStatus(ActivityStatusCode.Error, $"MessagePump: Failed to map message {message.Id} from {Channel.Name} with {Channel.RoutingKey} on thread # {Thread.CurrentThread.ManagedThreadId}");
                }
                catch (Exception e)
                {
                    Log.FailedToDispatchMessage2(s_logger, e, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                    IncrementUnacceptableMessageCount();
                    span?.SetStatus(ActivityStatusCode.Error,$"MessagePump: Failed to dispatch message '{message.Id}' from {Channel.Name} with {Channel.RoutingKey} on thread # {Environment.CurrentManagedThreadId}");
                }
                finally
                {
                    Tracer?.EndSpan(span);
                }

                await Acknowledge(message);

            } while (true);

            Log.FinishedRunningMessageLoop(s_logger, Channel.Name, Channel.RoutingKey, Thread.CurrentThread.ManagedThreadId);
            Tracer?.EndSpan(pumpSpan);
        }

        private async Task InvokeDispatchRequest(IRequest request, Message message, RequestContext context)
        {
            // NOTE: DispatchRequest<TRequest> is a generic method constrained to TRequest : class, IRequest, but at runtime
            // we only have an IRequest reference due to the dynamic type lookup. To call the generic method with the actual
            // runtime type (e.g., MyEvent), we need to use reflection to construct and invoke the generic method with the correct type.

            try
            {
                MethodInfo? dispatchMethod = MakeDispatchMethod(request);

                await (Task)dispatchMethod.Invoke(this, [message.Header, request, context])!;
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
                dispatchMethod = typeof(Proactor)
                    .GetMethod(nameof(DispatchRequest), BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.MakeGenericMethod(requestType);
                DispatchMethodCache[requestType] = dispatchMethod!;
            }

            if (dispatchMethod is null)
            {
                throw new InvalidOperationException($"Could not find DispatchRequest method for type {requestType.FullName}");
            }

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
                var pipelineFactory =
                    typeof(TransformPipelineBuilderAsync).GetMethod(nameof(TransformPipelineBuilderAsync.BuildUnwrapPipeline), Type.EmptyTypes);

                // Make the generic method with the runtime type
                typedPipelineFactory = pipelineFactory!.MakeGenericMethod(requestType);
                UnWrapPipelineFactoryCache[requestType] = typedPipelineFactory;
            }

            // Invoke the method to get the pipeline instance
            var pipeline = typedPipelineFactory.Invoke(_transformPipelineBuilder, null);
            return pipeline;
        }

        private RequestContext InitRequestContext(Activity? span, Message message)
        {
            var context = RequestContextFactory.Create();
            context.Span = span;
            context.OriginatingMessage = message;
            context.Bag.AddOrUpdate("ChannelName", Channel.Name, (_, _) => Channel.Name);
            context.Bag.AddOrUpdate("RequestStart", PumpTimeProvider.GetUtcNow(), (_, _) => PumpTimeProvider.GetUtcNow());
            return context;
        }

        private Task<bool> RejectMessage(Message message, MessageRejectionReason reason )
        {
            Log.RejectingMessage(s_logger, message.Id, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
            
            message.Header.Bag[Message.RejectionReasonHeaderName] = $"Message rejected reason: {reason.RejectionReason} Description: {reason.Description}";
            
            return Channel.RejectAsync(message, reason);
        }

        private Task<bool> RequeueMessage(Message message)
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

                    IncrementUnacceptableMessageCount(); 
                    return RejectMessage(message, new MessageRejectionReason(
                        RejectionReason.DeliveryError, 
                        $"Handle Count Exceeded for message {originalMessageId}")
                    );
                }
            }

            Log.ReQueueingMessage(s_logger, message.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name, Channel.RoutingKey);

            return Channel.RequeueAsync(message, RequeueDelay);
        }
        
        private async Task<IRequest> TranslateMessage(Message message, RequestContext requestContext, CancellationToken cancellationToken = default)
        {
            Log.TranslateMessage(s_logger, message.Id, Thread.CurrentThread.ManagedThreadId);
            requestContext.Span?.AddEvent(new ActivityEvent("Translate Message"));

            var requestType = _mapRequestType(message);
            if (requestType == null)
                throw new MessageMappingException($"Failed to find request type for message {message.Id} ", 
                    new ArgumentNullException(nameof(requestType), "The request type cannot be null."));

            try
            {
                object? pipeline = MakeUnwrapPipeline(requestType);

                // Call UnwrapAsync on the pipeline
                var unwrapMethod = pipeline!.GetType().GetMethod("UnwrapAsync");
                var task = (Task)unwrapMethod!.Invoke(pipeline, [message, requestContext, cancellationToken])!;
                await task.ConfigureAwait(false);
                var resultProperty = task.GetType().GetProperty("Result");
                return (IRequest)resultProperty!.GetValue(task)!;
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (TargetInvocationException tie)
            {
                // Unwrap exceptions thrown from reflected method invocation
                var innerException = tie.InnerException;
                if (innerException is InvalidMessageAction or RejectMessageAction or DeferMessageAction or DontAckAction)
                    throw innerException;
                throw new MessageMappingException($"Failed to map message {message.Id} of {requestType.FullName} using transform pipeline ", innerException ?? tie);
            }
            catch (Exception exception) when (exception is InvalidMessageAction or RejectMessageAction or DeferMessageAction)
            {
                // Re-throw action exceptions directly so they are handled by the message pump
                throw;
            }
            catch (Exception exception)
            {
                throw new MessageMappingException($"Failed to map message {message.Id} of {requestType.FullName} using transform pipeline ", exception);
            }
        }

        private bool UnacceptableMessageLimitReached()
        {
            if (UnacceptableMessageLimit <= 0) return false;
            if (UnacceptableMessageCount < UnacceptableMessageLimit) return false;
            
            Log.UnacceptableMessageLimitReached(s_logger, UnacceptableMessageLimit, Channel.Name, Channel.RoutingKey, Environment.CurrentManagedThreadId);
                
            return true;
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "MessagePump: Acknowledge message {Id} read from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void AcknowledgeMessage(ILogger logger, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Debug, "MessagePump: Dispatching message {Id} from {ChannelName} on thread # {ManagementThreadId}")]
            public static partial void DispatchingMessage(ILogger logger, string id, int managementThreadId, string? channelName);
            
            [LoggerMessage(LogLevel.Debug, "MessagePump: Receiving messages from channel {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void ReceivingMessagesFromChannel(ILogger logger, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Warning, "MessagePump: BrokenCircuitException messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void BrokenCircuitExceptionMessages(ILogger logger, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Warning, "MessagePump: ChannelFailureException messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void ChannelFailureExceptionMessages(ILogger logger, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Error, "MessagePump: Exception receiving messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void ExceptionReceivingMessages(ILogger logger, Exception ex, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Warning, "MessagePump: Failed to parse a message from the incoming message with id {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void FailedToParseMessage(ILogger logger, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Information, "MessagePump: Quit receiving messages from {ChannelName} on thread #{ManagementThreadId}")]
            public static partial void QuitReceivingMessages(ILogger logger, string? channelName, int managementThreadId);
            
            [LoggerMessage(LogLevel.Critical, "MessagePump: Stopping receiving of messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void StoppingReceivingMessages(ILogger logger, Exception ex, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Error, "MessagePump: Failed to dispatch message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void FailedToDispatchMessage(ILogger logger, Exception ex, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Debug, "MessagePump: Deferring message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void DeferringMessage(ILogger logger, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Critical, "MessagePump: Stopping receiving of messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void StoppingReceivingMessages2(ILogger logger, ConfigurationException ex, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Debug, "MessagePump: Deferring message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void DeferringMessage2(ILogger logger, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Warning, "MessagePump: Failed to map message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void FailedToMapMessage(ILogger logger, MessageMappingException ex, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Error, "MessagePump: Failed to dispatch message '{Id}' from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void FailedToDispatchMessage2(ILogger logger, Exception ex, string id, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Information, "MessagePump0: Finished running message loop, no longer receiving messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void FinishedRunningMessageLoop(ILogger logger, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Debug, "MessagePump: Translate message {Id} on thread # {ManagementThreadId}")]
            public static partial void TranslateMessage(ILogger logger, string id, int managementThreadId);
            
            [LoggerMessage(LogLevel.Warning, "MessagePump: Rejecting message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void RejectingMessage(ILogger logger, string id, string? channelName, string routingKey, int managementThreadId);

            [LoggerMessage(LogLevel.Error, "MessagePump: Have tried {RequeueCount} times to handle this message {Id}{OriginalMessageId} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}, dropping message.")]
            public static partial void DroppingMessage(ILogger logger, int requeueCount, string id, string originalMessageId, string? channelName, string routingKey, int managementThreadId);
            
            [LoggerMessage(LogLevel.Debug, "MessagePump: Re-queueing message {Id} from {ManagementThreadId} on thread # {ChannelName} with {RoutingKey}")]
            public static partial void ReQueueingMessage(ILogger logger, string id, int managementThreadId, ChannelName channelName, string routingKey);
            
            [LoggerMessage(LogLevel.Warning, "MessagePump: Not acknowledging message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void NotAcknowledgingMessage(ILogger logger, string id, string? channelName, string routingKey, int managementThreadId);

            [LoggerMessage(LogLevel.Error, "MessagePump: DontAckAction inner exception for message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            internal static partial void DontAckActionInnerException(ILogger logger, Exception ex, string id, string? channelName, string routingKey, int managementThreadId);

            [LoggerMessage(LogLevel.Critical, "MessagePump: Unacceptable message limit of {UnacceptableMessageLimit} reached, stopping reading messages from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}")]
            public static partial void UnacceptableMessageLimitReached(ILogger logger, int unacceptableMessageLimit, string? channelName, string routingKey, int managementThreadId);
        }
    }
}

