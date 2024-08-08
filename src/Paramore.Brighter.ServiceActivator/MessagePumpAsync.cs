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
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Used when we don't want to block for I/O, but queue on a completion port and be notified when done
    /// Adopts a single-threaded apartment model. We have one thread, all work - messages and calbacks is queued to that a single work queue
    /// When a callback is signalled it is queued next, and will be picked up when the current message completes or waits itself
    /// Strict ordering of messages will be lost as no guarantee what order I/O operations will complete - do not use if strict ordering required
    /// Only used one thread, so predictable performance, but may have many messages queued. Once queue length exceeds buffer size, we will stop reading new work
    /// Based on https://devblogs.microsoft.com/pfxteam/await-synchronizationcontext-and-console-apps/
    /// </summary>
    /// <typeparam name="TRequest">The Request on the Data Type Channel</typeparam>
    public class MessagePumpAsync<TRequest> : MessagePump<TRequest> where TRequest : class, IRequest
    {
        private readonly UnwrapPipelineAsync<TRequest> _unwrapPipeline;

        /// <summary>
        /// Constructs a message pump 
        /// </summary>
        /// <param name="commandProcessorProvider">Provides a way to grab a command processor correctly scoped</param>
        /// <param name="messageMapperRegistry">The registry of mappers</param>
        /// <param name="messageTransformerFactory">The factory that lets us create instances of transforms</param>
        /// <param name="requestContextFactory">A factory to create instances of request context, used to add context to a pipeline</param>
        /// <param name="tracer">What is the tracer we will use for telemetry</param>
        /// <param name="instrumentationOptions">When creating a span for <see cref="CommandProcessor"/> operations how noisy should the attributes be</param>
        public MessagePumpAsync(
            IAmACommandProcessorProvider commandProcessorProvider,
            IAmAMessageMapperRegistryAsync messageMapperRegistry, 
            IAmAMessageTransformerFactoryAsync messageTransformerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmABrighterTracer tracer = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All) 
            : base(commandProcessorProvider, requestContextFactory, tracer, instrumentationOptions)
        {
            var transformPipelineBuilder = new TransformPipelineBuilderAsync(messageMapperRegistry, messageTransformerFactory);
            _unwrapPipeline = transformPipelineBuilder.BuildUnwrapPipeline<TRequest>();
        }

        protected override void DispatchRequest(MessageHeader messageHeader, TRequest request, RequestContext requestContext)
        {
            s_logger.LogDebug("MessagePump: Dispatching message {Id} from {ChannelName} on thread # {ManagementThreadId}", request.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);
            requestContext.Span?.AddEvent(new ActivityEvent("Dispatch Message"));

            var messageType = messageHeader.MessageType;
            
            ValidateMessageType(messageType, request);

            switch (messageType)
            {
                case MessageType.MT_COMMAND:
                {
                    RunDispatch(SendAsync, request, requestContext);
                    break;
                }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                {
                    RunDispatch(PublishAsync, request, requestContext);
                    break;
                }
            }
        }

        protected override TRequest TranslateMessage(Message message, RequestContext requestContext)
        {
            s_logger.LogDebug(
                "MessagePump: Translate message {Id} on thread # {ManagementThreadId}", 
                message.Id, Thread.CurrentThread.ManagedThreadId
            );
            requestContext.Span?.AddEvent(new ActivityEvent("Translate Message"));
            
            return RunTranslate(TranslateAsync, message, requestContext);
        }

        private static void RunDispatch(
            Action<TRequest, RequestContext, CancellationToken> act, TRequest request, 
            RequestContext requestContext, 
            CancellationToken cancellationToken = default
        )
        {
            if (act == null) throw new ArgumentNullException(nameof(act));

            var prevCtx = SynchronizationContext.Current;
            
            try
            {
                // Establish the new context
                var context = new BrighterSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(context);
                
                context.OperationStarted();
                
                act(request, requestContext, cancellationToken);

                context.OperationCompleted();

                // Pump continuations and propagate any exceptions
                context.RunOnCurrentThread();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevCtx);
            }
        }
        
        private static TRequest RunTranslate(
            Func<Message, RequestContext, CancellationToken, Task<TRequest>> act, 
            Message message, 
            RequestContext requestContext,
            CancellationToken cancellationToken = default
        )
        {
            if (act == null) throw new ArgumentNullException(nameof(act));

            var prevCtx = SynchronizationContext.Current;
            try
            {
                // Establish the new context
                var context = new BrighterSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(context);

                context.OperationStarted();

                var future = act(message, requestContext, cancellationToken);
                
                future.ContinueWith(delegate { context.OperationCompleted(); }, TaskScheduler.Default);

                // Pump continuations and propagate any exceptions
                context.RunOnCurrentThread();

                return future.GetAwaiter().GetResult();
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new MessageMappingException($"Failed to map message {message.Id} using pipeline for type {typeof(TRequest).FullName} ", exception);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevCtx);
            }
        }
        
        private async void PublishAsync(TRequest request, RequestContext requestContext, CancellationToken cancellationToken = default)
        {
            await CommandProcessorProvider.Get().PublishAsync(request, requestContext, continueOnCapturedContext: true, cancellationToken);
        }

        private async void SendAsync(TRequest request, RequestContext requestContext, CancellationToken cancellationToken = default)
        {
            await CommandProcessorProvider.Get().SendAsync(request,requestContext, continueOnCapturedContext: true, cancellationToken);
        }

        private async Task<TRequest> TranslateAsync(Message message, RequestContext requestContext, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _unwrapPipeline.UnwrapAsync(message, requestContext, cancellationToken);
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new MessageMappingException($"Failed to map message {message.Id} using pipeline for type {typeof(TRequest).FullName} ", exception);
            }
        }
    }
}
