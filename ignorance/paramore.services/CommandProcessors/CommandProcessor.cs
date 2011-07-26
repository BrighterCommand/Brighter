using System;
using System.ComponentModel;
using System.Linq;
using Castle.Windsor;
using Paramore.Services.Common;

namespace Paramore.Services.CommandProcessors
{
    public class CommandProcessor
    {
        private readonly IWindsorContainer _container;

        public CommandProcessor(IWindsorContainer container)
        {
            _container = container;
        }

        public void Send<T>(T command) where T : class, IRequest
        {
            var builder = new ChainofResponsibilityBuilder<T>(_container);
            var handlerChain = builder.Build();

            var handlerCount = handlerChain.Count();

            if (handlerCount > 1)
                throw new ArgumentException(string.Format("More than one handler was found for the typeof command {0} - a command should only have one handler.", typeof(T)));
            if (handlerCount == 0)
                throw new ArgumentException(string.Format("No command handler was found for the typeof command {0} - a command should have only one handler.", typeof(T))); 

            handlerChain.First().Handle(command);
        }
    }
}