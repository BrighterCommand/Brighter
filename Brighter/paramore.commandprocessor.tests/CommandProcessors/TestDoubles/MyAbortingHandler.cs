using System;
using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    public class MyAbortingHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        public override TRequest Handle(TRequest command)
        {
           throw new Exception("Aborting chain"); 
        }
    }
}
