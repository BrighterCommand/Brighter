using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator
{
    public class CommandProcessorConfiguration
    {
        public CommandProcessorConfiguration(IAmARequestContextFactory  requestContextFactory)
        {
            RequestContextFactory = requestContextFactory;
        }

        public IAmARequestContextFactory RequestContextFactory { get; private set; }
    }
}