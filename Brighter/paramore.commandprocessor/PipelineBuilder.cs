using System;
using System.Collections.Generic;
using System.Linq;

namespace paramore.brighter.commandprocessor
{
    internal class PipelineBuilder<TRequest> : IAmAPipelineBuilder<TRequest> where TRequest : class, IRequest
    {
        private readonly IAdaptAnInversionOfControlContainer container;
        private readonly Interpreter<TRequest> interpreter;
        private readonly IDisposable instanceScope;
        private readonly IList<IHandleRequests<TRequest>> decorators = new List<IHandleRequests<TRequest>>(); 

        public PipelineBuilder(IAdaptAnInversionOfControlContainer  container)
        {
            this.container = container;
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