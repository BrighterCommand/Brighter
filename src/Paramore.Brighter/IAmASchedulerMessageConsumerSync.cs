namespace Paramore.Brighter;

public interface IAmASchedulerMessageConsumerSync : IAmASchedulerMessageConsumer 
{
    void Consume(Message message, RequestContext context);
}
