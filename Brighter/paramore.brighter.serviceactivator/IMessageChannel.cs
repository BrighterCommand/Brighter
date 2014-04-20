using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator
{
    public interface IMessageChannel
    {
        Message Listen(int timeoutinMilliseconds);
        void Enqueue(Message message);
        void AcknowledgeMessage(Message message);
    }
}