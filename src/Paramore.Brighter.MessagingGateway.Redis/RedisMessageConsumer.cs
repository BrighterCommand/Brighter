namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageConsumer : IAmAMessageConsumer
    {
        public void Dispose()
        {
            throw new System.NotImplementedException();
        }

        public Message Receive(int timeoutInMilliseconds)
        {
            throw new System.NotImplementedException();
        }

        public void Acknowledge(Message message)
        {
            throw new System.NotImplementedException();
        }

        public void Reject(Message message, bool requeue)
        {
            throw new System.NotImplementedException();
        }

        public void Purge()
        {
            throw new System.NotImplementedException();
        }

        public void Requeue(Message message)
        {
            throw new System.NotImplementedException();
        }
    }
}