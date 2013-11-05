using System;
using Tasklist.Ports.Commands;
using paramore.commandprocessor;

namespace Tasklist.Ports.Handlers
{
    public class ValidationHandler<TRequest> : RequestHandler<TRequest>
        where TRequest: class, IRequest, ICanBeValidated 
    {
        public override TRequest Handle(TRequest command)
        {
            if (!((ICanBeValidated)command).IsValid())
            {
                throw new ArgumentException("The commmand was not valid");
            }

            return base.Handle(command);
        }
    }
}