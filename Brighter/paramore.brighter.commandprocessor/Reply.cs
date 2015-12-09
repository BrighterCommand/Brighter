using System;

namespace paramore.brighter.commandprocessor
{
    public class Reply : Command
    {
        public ReplyAddress SendersAddress { get; private set; }

        public Reply(ReplyAddress sendersAddress)
            : base(Guid.NewGuid())
        {
            SendersAddress = sendersAddress;
        }
    }
}