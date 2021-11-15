namespace Paramore.Brighter.ServiceActivator
{
    public class CommandProcessorProvider : IAmACommandProcessorProvider
    {
        private readonly IAmACommandProcessor _commandProcessor;

        public CommandProcessorProvider(IAmACommandProcessor commandProcessor)
        {
            _commandProcessor = commandProcessor;
        }
        public void Dispose()
        {
            //Nothing to Dispose
        }

        public IAmACommandProcessor Get()
        {
            return _commandProcessor;
        }

        public void CreateScope()
        {
            //This is not Scoped
        }

        public void ReleaseScope()
        {
            //This is not scoped
        }
    }
}
