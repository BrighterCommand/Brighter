namespace paramore.commandprocessor
{
    public interface IHandleRequests<TRequest> where TRequest : class, IRequest
    {
        IRequestContext Context { get; set; }
        void DescribePath(IAmAPipelineTracer pathExplorer);
        TRequest Handle(TRequest command);
        void InitializeFromAttributeParams(params object[] initializerList);
        IHandleRequests<TRequest> Successor { set; }
    }
}
