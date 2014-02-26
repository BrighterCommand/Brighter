namespace paramore.commandprocessor
{
    public interface IHandleRequests<TRequest> where TRequest : class, IRequest
    {
        void DescribePath(IAmAPipelineTracer pathExplorer);
        TRequest Handle(TRequest command);
        IHandleRequests<TRequest> Successor { set; }
        IRequestContext Context { get; set; }
    }
}
