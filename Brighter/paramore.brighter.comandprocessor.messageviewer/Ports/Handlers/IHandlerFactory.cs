namespace paramore.brighter.commandprocessor.messageviewer.Ports.Handlers
{
    public interface IHandlerFactory
    {
        IHandleCommand<T> GetHandler<T>() where T : class, ICommand;
    }
}