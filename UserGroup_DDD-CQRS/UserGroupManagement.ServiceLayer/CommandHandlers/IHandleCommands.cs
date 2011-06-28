using UserGroupManagement.ServiceLayer.Commands;

namespace UserGroupManagement.ServiceLayer.CommandHandlers
{
    public interface IHandleCommands<in TCommand> where TCommand : class, ICommand
    {
        void Handle(TCommand command);
    }
}
