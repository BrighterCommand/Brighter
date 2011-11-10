using System;
using System.Linq;
using Paramore.Utility;
using TinyIoC;

namespace paramore.commandprocessor
{
    public class CommandProcessor
    {
        private readonly TinyIoCContainer container;

        public CommandProcessor(TinyIoCContainer  container)
        {
            this.container = container;
        }

        public void Send<T>(T command) where T : class, IRequest
        {
            var builder = new ChainofResponsibilityBuilder<T>(container);
            var handlerChain = builder.Build();

            var handlerCount = handlerChain.Count();

            if (handlerCount > 1)
                throw new ArgumentException(string.Format("More than one handler was found for the typeof command {0} - a command should only have one handler.", typeof(T)));
            if (handlerCount == 0)
                throw new ArgumentException(string.Format("No command handler was found for the typeof command {0} - a command should have only one handler.", typeof(T))); 

            handlerChain.First().Handle(command);
        }

        public void Publish<T>(T @event) where T : class, IRequest
        {
            var builder = new ChainofResponsibilityBuilder<T>(container);
            var handlerChain = builder.Build();

            handlerChain.Each(chain => chain.Handle(@event));
        }
    }
}