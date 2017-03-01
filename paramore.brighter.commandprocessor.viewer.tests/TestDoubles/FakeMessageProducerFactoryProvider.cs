using paramore.brighter.commandprocessor.messageviewer.Ports.Handlers;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    internal class FakeMessageProducerFactoryProvider : IMessageProducerFactoryProvider
    {
        private readonly IAmAMessageProducerFactory _aFactory;

        public FakeMessageProducerFactoryProvider(IAmAMessageProducerFactory aFactory)
        {
            _aFactory = aFactory;
        }

        public IAmAMessageProducerFactory Get()
        {
            return _aFactory;
        }
    }

}