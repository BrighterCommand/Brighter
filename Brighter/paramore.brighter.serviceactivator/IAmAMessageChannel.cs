using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator
{
    public interface IAmAMessageChannel
    {
        Message Listen(int timeoutinMilliseconds);
        void Enqueue(Message message);
        void AcknowledgeMessage(Message message);
        int Length { get; }
    }
}