using System.Collections.Generic;
using System.Linq;

namespace paramore.commandprocessor
{
    internal class PipelineBuilder<TRequest> : IAmAPipelineBuilder<TRequest> where TRequest : class, IRequest
    {
        private readonly IAdaptAnInversionOfControlContainer container;
        private readonly Interpreter<TRequest> interpreter;

        public PipelineBuilder(IAdaptAnInversionOfControlContainer  container)
        {
            this.container = container;
            interpreter = new Interpreter<TRequest>(container);
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
                lastInPipeline.Successor = decorator;
                lastInPipeline = decorator;
            }
        }

        private IHandleRequests<TRequest> PushOntoPipeline(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> lastInPipeline, IRequestContext requestContext)
        {
            foreach (var attribute in attributes) 
            {
                var decorator = new HandlerFactory<TRequest>(attribute, container, requestContext).CreateRequestHandler();
                decorator.Successor = lastInPipeline;
                lastInPipeline = decorator;
            }
            return lastInPipeline;
        }
    }
}