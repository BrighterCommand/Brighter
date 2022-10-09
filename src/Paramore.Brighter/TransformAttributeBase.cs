using System;

namespace Paramore.Brighter
{
    public abstract class TransformAttribute : Attribute
    {
        /// <summary>
        /// The order in which we run this 
        /// </summary>
        /// <value>The step.</value>
        public int Step { get; set; }

        /// <summary>
        /// What type do we implement for the Transform in the Message Mapper Pipeline
        /// </summary>
        /// <returns>Type.</returns>
        public abstract Type GetHandlerType();
    }
}
