using paramore.brighter.commandprocessor;

namespace paramore.brighter.messagedispatcher
{
    public interface IMessageChannel
    {
        Message Listen(int timeoutinMilliseconds);
        void Enqueue(Message message);
    }
}