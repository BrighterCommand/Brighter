namespace UserGroupManagement.CommandHandlers
{
    public interface IHandleCommands<in TCommand> where TCommand : class
    {
        void Handle(TCommand command);
    }
}
