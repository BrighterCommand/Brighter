using System;

namespace paramore.brighter.commandprocessor
{
    public class MessageBody
    {
        public string Body { get; private set; }

        public MessageBody(string body)
        {
            Body = body;
        }
    }
}