namespace paramore.brighter.commandprocessor.messageviewer.Ports.Handlers
{
    public interface IHandleCommand<in T>
    {
        void Handle(T command);
    }
}