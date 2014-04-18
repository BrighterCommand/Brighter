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
using System.Linq;
using Common.Logging;

namespace paramore.brighter.commandprocessor
{
    internal class PipelineBuilder<TRequest> : IAmAPipelineBuilder<TRequest> where TRequest : class, IRequest
    {
        private readonly IAdaptAnInversionOfControlContainer container;
        private readonly ILog logger;
        private readonly Interpreter<TRequest> interpreter;
        private readonly IDisposable instanceScope;
        private readonly IList<IHandleRequests<TRequest>> decorators = new List<IHandleRequests<TRequest>>(); 

        public PipelineBuilder(IAdaptAnInversionOfControlContainer  container, ILog logger)
        {
            this.container = container;
            this.logger = logger;
            instanceScope = container.CreateLifetime();
            interpreter = new Interpreter<TRequest>(container);
        }

        public IEnumerable<IHandleRequests<TRequest>> Decorators
        {
            get { return decorators; }
        } 

        public Pipelines<TRequest> Build(IRequestContext requestContext)
        {
            var handlers = interpreter.GetHandlers(typeof(TRequest));
            
            var pipelines = new Pipelines<TRequest>();
            foreach (var handler in handlers)
            {
                pipelines.Add(BuildPipeline(handler, requestContext));
            }

            return pipelines;
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
            logger.Info(m => m("New handler pipeline created: {0}", TracePipeline(firstInPipeline)));
            return firstInPipeline;
        }

        private void AppendToPipeline(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> implicitHandler, IRequestContext requestContext)
        {
            IHandleRequests<TRequest> lastInPipeline = implicitHandler;
            foreach (var attribute in attributes) 
            {
                var decorator = new HandlerFactory<TRequest>(attribute, container, requestContext).CreateRequestHandler();
                decorators.Add(decorator);
                lastInPipeline.Successor = decorator;
                lastInPipeline = decorator;
            }
        }

        private IHandleRequests<TRequest> PushOntoPipeline(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> lastInPipeline, IRequestContext requestContext)
        {
            foreach (var attribute in attributes) 
            {
                var decorator = new HandlerFactory<TRequest>(attribute, container, requestContext).CreateRequestHandler();
                decorators.Add(decorator);
                decorator.Successor = lastInPipeline;
                lastInPipeline = decorator;
            }
            return lastInPipeline;
        }

        private PipelineTracer TracePipeline(IHandleRequests<TRequest> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        public void Dispose()
        {
            foreach (var decorator in decorators)
            {
                var disposable = decorator as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }

            decorators.Clear();
            instanceScope.Dispose();
        }
    }
}