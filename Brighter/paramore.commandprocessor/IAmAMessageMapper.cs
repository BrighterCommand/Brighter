namespace paramore.brighter.commandprocessor
{
    public interface IAmAMessageMapper<TRequest, TMessage> where TRequest : class, IRequest where TMessage: Message
    {
        TMessage Map(TRequest request);
    }
}