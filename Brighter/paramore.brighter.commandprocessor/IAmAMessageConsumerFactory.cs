namespace paramore.brighter.commandprocessor
{
    public interface IAmAMessageConsumerFactory
    {
        IAmAMessageConsumer Create(string queueName, string routingKey);
    }
}