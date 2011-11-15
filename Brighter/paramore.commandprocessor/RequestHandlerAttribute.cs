using System;

namespace paramore.commandprocessor
{
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class RequestHandlerAttribute : Attribute
    {
        private readonly int step;

        private readonly HandlerTiming timing;

        protected RequestHandlerAttribute(int step, HandlerTiming timing = HandlerTiming.Before)
        {
            this.step = step;
            this.timing = timing;
        }

        public int Step
        {
            get { return step; }
        }

        public HandlerTiming Timing
        {
            get { return timing; }
        }

        public abstract Type GetHandlerType();
    }
}