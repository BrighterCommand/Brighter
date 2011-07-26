using System;
using Paramore.Services.CommandHandlers;

namespace Paramore.Services.CommandProcessors
{
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class RequestHandlerAttribute : Attribute
    {
        private readonly int _step;

        private readonly HandlerTiming _timing;

        protected RequestHandlerAttribute(int step, HandlerTiming timing = HandlerTiming.Before)
        {
            _step = step;
            _timing = timing;
        }

        public int Step
        {
            get { return _step; }
        }

        public HandlerTiming Timing
        {
            get { return _timing; }
        }

        public abstract Type GetHandlerType();
    }
}