using System;
using System.Linq;
using paramore.commandprocessor.extensions;

namespace paramore.commandprocessor
{
    public class CommandProcessor : IAmACommandProcessor
    {
        private readonly IAdaptAnInversionOfControlContainer container;
        private readonly IAmARequestContextFactory requestContextFactory;

        public CommandProcessor(IAdaptAnInversionOfControlContainer container, IAmARequestContextFactory requestContextFactory)
        {
            this.container = container;
            this.requestContextFactory = requestContextFactory;
        }

        public void Send<T>(T command) where T : class, IRequest
        {
            var builder = new PipelineBuilder<T>(container);
            var requestContext = requestContextFactory.Create(container);
            var handlerChain = builder.Build(requestContext);

            var handlerCount = handlerChain.Count();

            if (handlerCount > 1)
                throw new ArgumentException(string.Format("More than one handler was found for the typeof command {0} - a command should only have one handler.", typeof(T)));
            if (handlerCount == 0)
                throw new ArgumentException(string.Format("No command handler was found for the typeof command {0} - a command should have only one handler.", typeof(T))); 

            handlerChain.First().Handle(command);
        }

        public void Publish<T>(T @event) where T : class, IRequest
        {
            var builder = new PipelineBuilder<T>(container);
            var requestContext = new RequestContext(container);
            var handlerChain = builder.Build(requestContext);

            handlerChain.Each(chain => chain.Handle(@event));
        }
    }
}