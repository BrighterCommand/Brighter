using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messageviewer.Ports.Handlers
{
    public interface IMessageProducerFactoryProvider
    {
        IAmAMessageProducerFactory Get(ILog logger);
    }
}