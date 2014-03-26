namespace paramore.brighter.commandprocessor
{
    public interface IAmAMessageMapper<TCommand, TCommandMessage> where TCommand : class, IRequest where TCommandMessage: CommandMessage
    {
    }
}