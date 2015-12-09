using System;

namespace paramore.brighter.commandprocessor
{
    public class Request : Command
    {
        public ReplyAddress ReplyAddress { get; private set; }

        public Request(ReplyAddress replyAddress)
            : base(Guid.NewGuid())
        {
            ReplyAddress = replyAddress;
        }
    }
}
