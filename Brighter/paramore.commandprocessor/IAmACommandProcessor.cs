namespace paramore.brighter.commandprocessor
{
    public interface IAmACommandProcessor
    {
        void Send<T>(T command) where T : class, IRequest;
        void Publish<T>(T @event) where T : class, IRequest;
    }
}