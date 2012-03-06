namespace paramore.commandprocessor
{
    public class InMemoryRequestContextFactory : IAmARequestContextFactory
    {
        public RequestContext Create()
        {
            return new RequestContext();
        }
    }
}
