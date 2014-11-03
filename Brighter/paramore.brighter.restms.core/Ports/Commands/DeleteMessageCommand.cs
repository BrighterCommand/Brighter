using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class DeleteMessageCommand : Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="id">The identifier.</param>
        public DeleteMessageCommand() : base(Guid.NewGuid())
        {
        }
    }
}
