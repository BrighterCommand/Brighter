﻿#region Licence
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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Used when the message pump should block for I/O
    /// Will guarantee strict ordering of the messages on the queue
    /// Predictable performance as only one thread, allows you to configure number of performers for number of threads to use
    /// Lower throughput than async
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    public class MessagePumpBlocking<TRequest> : MessagePump<TRequest> where TRequest : class, IRequest
    {
        private readonly UnwrapPipeline<TRequest> _unwrapPipeline;

        /// <summary>
        /// Constructs a message pump 
        /// </summary>
        /// <param name="commandProcessorProvider">Provides a way to grab a command processor correctly scoped</param>
        /// <param name="messageMapperRegistry">The registry of mappers</param>
        /// <param name="messageTransformerFactory">The factory that lets us create instances of transforms</param>
        /// <param name="requestContextFactory">A factory to create instances of request context, used to add context to a pipeline</param>
        /// <param name="tracer">What is the tracer we will use for telemetry</param>
        /// <param name="instrumentationOptions">When creating a span for <see cref="CommandProcessor"/> operations how noisy should the attributes be</param>
        public MessagePumpBlocking(
            IAmACommandProcessorProvider commandProcessorProvider,
            IAmAMessageMapperRegistry messageMapperRegistry, 
            IAmAMessageTransformerFactory messageTransformerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmAChannel channel,
            IAmABrighterTracer? tracer = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All) 
            : base(commandProcessorProvider, requestContextFactory, tracer, channel, instrumentationOptions)
        {
            var transformPipelineBuilder = new TransformPipelineBuilder(messageMapperRegistry, messageTransformerFactory);
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
                    CommandProcessorProvider.Get().Send(request, requestContext);
                    break;
                }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                {
                    CommandProcessorProvider.Get().Publish(request, requestContext);
                    break;
                }
            }
        }

        protected override TRequest TranslateMessage(Message message, RequestContext requestContext)
        {
            s_logger.LogDebug("MessagePump: Translate message {Id} on thread # {ManagementThreadId}", message.Id, Thread.CurrentThread.ManagedThreadId);
            requestContext.Span?.AddEvent(new ActivityEvent("Translate Message"));

            TRequest request;

            try
            {
                request = _unwrapPipeline.Unwrap(message, requestContext);
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new MessageMappingException($"Failed to map message {message.Id} using pipeline for type {typeof(TRequest).FullName} ", exception);
            }

            return request;
        }
    }
}
