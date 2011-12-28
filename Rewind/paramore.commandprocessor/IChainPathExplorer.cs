namespace paramore.commandprocessor
{
    public interface IChainPathExplorer
    {
        void AddToPath(HandlerName handlerName);
        string ToString();
    }
}