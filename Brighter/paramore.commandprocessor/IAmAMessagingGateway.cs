namespace paramore.brighter.commandprocessor
{
    public interface IAmAMessagingGateway
    {
        void SendMessage(CommandMessage commandMessage);
    }
}