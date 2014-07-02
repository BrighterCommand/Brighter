namespace paramore.brighter.commandprocessor
{
    public class HandlerConfiguration
    {
        public IAmATargetHandlerRegistry TargetHandlerRegistry { get; private set; }
        public IAmAHandlerFactory HandlerFactory { get; private set; }

        public HandlerConfiguration(IAmATargetHandlerRegistry targetHandlerRegistry, IAmAHandlerFactory handlerFactory)
        {
            TargetHandlerRegistry = targetHandlerRegistry;
            HandlerFactory = handlerFactory;
        }
    }
}
