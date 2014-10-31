using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class DeletePipeCommand : Command
    {
        public string PipeName { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="pipeName"></param>
        public DeletePipeCommand(string pipeName) : base(Guid.NewGuid())
        {
            PipeName = pipeName;
        }
    }
}
