using System;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    internal class FakeErrorProducingMessageProducerFactory : IAmAMessageProducerFactory
    {
        public IAmAMessageProducer Create()
        {
            throw new NotImplementedException();
        }
    }
}