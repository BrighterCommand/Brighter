using Paramore.Brighter.MessageViewer.Ports.Handlers;

namespace Paramore.Brighter.Viewer.Tests.TestDoubles
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