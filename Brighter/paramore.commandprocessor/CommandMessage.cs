namespace paramore.brighter.commandprocessor
{
    public class CommandMessage
    {
        public MessageHeader Header { get; private set; }
        public MessageBody Body { get; private set; }

        public CommandMessage(MessageHeader header, MessageBody body)
        {
            Header = header;
            Body = body;
        }
    }
}