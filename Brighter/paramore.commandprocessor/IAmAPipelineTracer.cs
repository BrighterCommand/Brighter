namespace paramore.commandprocessor
{
    public interface IAmAPipelineTracer
    {
        void AddToPath(HandlerName handlerName);
        string ToString();
    }
}