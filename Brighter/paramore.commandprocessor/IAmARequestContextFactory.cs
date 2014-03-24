namespace paramore.brighter.commandprocessor
{
    public interface IAmARequestContextFactory
    {
        RequestContext Create(IAdaptAnInversionOfControlContainer container);
    }
}