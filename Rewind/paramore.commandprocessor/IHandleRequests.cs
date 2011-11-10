namespace paramore.commandprocessor
{
    public interface IHandleRequests<TRequest> where TRequest : class, IRequest
    {
        void DescribePath(ChainPathExplorer pathExplorer);
        TRequest Handle(TRequest command);
        IHandleRequests<TRequest> Successor { set; }
    }
}
