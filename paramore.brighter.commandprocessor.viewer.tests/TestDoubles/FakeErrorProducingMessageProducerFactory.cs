using System;

namespace Paramore.Brighter.Viewer.Tests.TestDoubles
{
    internal class FakeErrorProducingMessageProducerFactory : IAmAMessageProducerFactory
    {
        public IAmAMessageProducer Create()
        {
            throw new NotImplementedException();
        }
    }
}