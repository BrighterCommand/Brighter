using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class DeleteMessageCommand : Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="pipeName"></param>
        /// <param name="messageId"></param>
        public DeleteMessageCommand(string pipeName, Guid messageId) : base(Guid.NewGuid())
        {
            PipeName = pipeName;
            MessageId = messageId;
        }

        public string PipeName { get; private set; }
        public Guid MessageId { get; private set; }
    }
}
