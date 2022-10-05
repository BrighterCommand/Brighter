using System;

namespace Paramore.Brighter
{
    public abstract class UnwrapWithAttribute
    {
        private int _step;

        protected UnwrapWithAttribute(int step)
        {
            _step = step;
        }
        
        //In which order should we run this
        /// <summary>
        /// Gets the step.
        /// </summary>
        /// <value>The step.</value>
        public int Step
        {
            get { return _step; }
            set { _step = value; }
        }    
        
        //What type do we implement for the Transform in the Message Mapper Pipeline
        /// <summary>
        /// Gets the type of the handler.
        /// </summary>
        /// <returns>Type.</returns>
        public abstract Type GetHandlerType();
    }
}
