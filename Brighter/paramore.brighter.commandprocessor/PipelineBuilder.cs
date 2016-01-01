// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-01-2014
// ***********************************************************************
// <copyright file="Pipelines.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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

using System.Collections.Generic;
using System.Linq;
using paramore.brighter.commandprocessor.extensions;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor
{
    internal class PipelineBuilder<TRequest> : IAmAPipelineBuilder<TRequest> where TRequest : class, IRequest
    {
        private readonly IAmAHandlerFactory _handlerFactory;
        private readonly ILog _logger;
        private readonly Interpreter<TRequest> _interpreter;
        private readonly IAmALifetime _instanceScope;
        private readonly IAmAnAsyncHandlerFactory _asyncHandlerFactory;

        internal PipelineBuilder(IAmASubscriberRegistry registry, IAmAHandlerFactory handlerFactory) 
            :this(registry, handlerFactory, LogProvider.GetCurrentClassLogger())
        {}


        internal PipelineBuilder(IAmASubscriberRegistry registry, IAmAHandlerFactory handlerFactory, ILog logger)
        {
            _handlerFactory = handlerFactory;
            _logger = logger;
            _instanceScope = new LifetimeScope(handlerFactory);
            _interpreter = new Interpreter<TRequest>(registry, handlerFactory);
        }

        internal PipelineBuilder(IAmASubscriberRegistry registry, IAmAnAsyncHandlerFactory asyncHandlerFactory)
            : this(registry, asyncHandlerFactory, LogProvider.GetCurrentClassLogger())
        { }

        public PipelineBuilder(IAmASubscriberRegistry registry, IAmAnAsyncHandlerFactory asyncHandlerFactory, ILog logger)
        {
            _asyncHandlerFactory = asyncHandlerFactory;
            _logger = logger;
            _instanceScope = new LifetimeScope(asyncHandlerFactory);
            _interpreter = new Interpreter<TRequest>(registry, asyncHandlerFactory);
        }

        public Pipelines<TRequest> Build(IRequestContext requestContext)
        {
            var handlers = _interpreter.GetHandlers(typeof(TRequest));

            var pipelines = new Pipelines<TRequest>();
            handlers.Each((handler) => pipelines.Add(BuildPipeline(handler, requestContext)));

            pipelines.Each((handler) => handler.AddToLifetime(_instanceScope));

            return pipelines;
        }

        public void Dispose()
        {
            _instanceScope.Dispose();
        }

        private IHandleRequests<TRequest> BuildPipeline(RequestHandler<TRequest> implicitHandler, IRequestContext requestContext)
        {
            implicitHandler.Context = requestContext;

            var preAttributes =
                implicitHandler.FindHandlerMethod()
                .GetOtherHandlersInPipeline()
                .Where(attribute => attribute.Timing == HandlerTiming.Before)
                .OrderByDescending(attribute => attribute.Step);

            var firstInPipeline = PushOntoPipeline(preAttributes, implicitHandler, requestContext);

            var postAttributes =
                implicitHandler.FindHandlerMethod()
                .GetOtherHandlersInPipeline()
                .Where(attribute => attribute.Timing == HandlerTiming.After)
                .OrderByDescending(attribute => attribute.Step);

            AppendToPipeline(postAttributes, implicitHandler, requestContext);
            _logger.DebugFormat("New handler pipeline created: {0}", TracePipeline(firstInPipeline));
            return firstInPipeline;
        }

        private void AppendToPipeline(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> implicitHandler, IRequestContext requestContext)
        {
            IHandleRequests<TRequest> lastInPipeline = implicitHandler;
            attributes.Each((attribute) =>
            {
                var decorator = new HandlerFactory<TRequest>(attribute, _handlerFactory, requestContext).CreateRequestHandler();
                lastInPipeline.SetSuccessor(decorator);
                lastInPipeline = decorator;
            });
        }

        private IHandleRequests<TRequest> PushOntoPipeline(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> lastInPipeline, IRequestContext requestContext)
        {
            attributes.Each((attribute) =>
            {
                var decorator = new HandlerFactory<TRequest>(attribute, _handlerFactory, requestContext).CreateRequestHandler();
                decorator.SetSuccessor(lastInPipeline);
                lastInPipeline = decorator;
            });
            return lastInPipeline;
        }

        public AsyncPipelines<TRequest> BuildAsync(IRequestContext requestContext)
        {
            var handlers = _interpreter.GetAsyncHandlers(typeof(TRequest));

            var pipelines = new AsyncPipelines<TRequest>();
            handlers.Each(handler => pipelines.Add(BuildAsyncPipeline(handler, requestContext)));

            pipelines.Each(handler => handler.AddToLifetime(_instanceScope));

            return pipelines;
        }

        private IHandleRequestsAsync<TRequest> BuildAsyncPipeline(AsyncRequestHandler<TRequest> implicitHandler, IRequestContext requestContext)
        {
            implicitHandler.Context = requestContext;

            var preAttributes =
                implicitHandler.FindHandlerMethod()
                .GetOtherAsyncHandlersInPipeline()
                .Where(attribute => attribute.Timing == HandlerTiming.Before)
                .OrderByDescending(attribute => attribute.Step);

            var firstInPipeline = PushOntoAsyncPipeline(preAttributes, implicitHandler, requestContext);

            var postAttributes =
                implicitHandler.FindHandlerMethod()
                .GetOtherAsyncHandlersInPipeline()
                .Where(attribute => attribute.Timing == HandlerTiming.After)
                .OrderByDescending(attribute => attribute.Step);

            AppendToAsyncPipeline(postAttributes, implicitHandler, requestContext);
            _logger.DebugFormat("New handler pipeline created: {0}", TracePipeline(firstInPipeline));
            return firstInPipeline;
        }

        private void AppendToAsyncPipeline(IEnumerable<AsyncRequestHandlerAttribute> attributes, IHandleRequestsAsync<TRequest> implicitHandler, IRequestContext requestContext)
        {
            IHandleRequestsAsync<TRequest> lastInPipeline = implicitHandler;
            attributes.Each(attribute =>
            {
                var decorator = new AsyncHandlerFactory<TRequest>(attribute, _asyncHandlerFactory, requestContext).CreateAsyncRequestHandler();
                lastInPipeline.SetSuccessor(decorator);
                lastInPipeline = decorator;
            });
        }

        private IHandleRequestsAsync<TRequest> PushOntoAsyncPipeline(IEnumerable<AsyncRequestHandlerAttribute> attributes, IHandleRequestsAsync<TRequest> lastInPipeline, IRequestContext requestContext)
        {
            attributes.Each(attribute =>
            {
                var decorator = new AsyncHandlerFactory<TRequest>(attribute, _asyncHandlerFactory, requestContext).CreateAsyncRequestHandler();
                decorator.SetSuccessor(lastInPipeline);
                lastInPipeline = decorator;
            });
            return lastInPipeline;
        }

        private PipelineTracer TracePipeline(IHandleRequests<TRequest> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private PipelineTracer TracePipeline(IHandleRequestsAsync<TRequest> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}