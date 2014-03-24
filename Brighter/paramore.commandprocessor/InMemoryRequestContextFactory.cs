namespace paramore.brighter.commandprocessor
{
    public class InMemoryRequestContextFactory : IAmARequestContextFactory
    {
        public RequestContext Create(IAdaptAnInversionOfControlContainer container)
        {
            return new RequestContext(container);
        }
    }
}
