using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class InvalidateCacheCommand : Command
    {
        public Uri ResourceToInvalidate { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        public InvalidateCacheCommand(Uri resourceToInvalidate) : base(Guid.NewGuid())
        {
            this.ResourceToInvalidate = resourceToInvalidate;
        }
    }
}
