using System;

namespace paramore.brighter.commandprocessor
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

        //We use this to pass params from the attribute into the instance of the handler
        //if you need to pass additional params to your handler, use this
        public virtual object[] InitializerParams()
        {
            return new object[0];
        }

        //In which order should we run this, within the pre or post sequence for the main target?
        public int Step
        {
            get { return step; }
        }

        //Should we run this before or after the main target?
        public HandlerTiming Timing
        {
            get { return timing; }
        }

        //What type do we implement for the Filter in the Command Processor Pipeline
        public abstract Type GetHandlerType();
    }
}