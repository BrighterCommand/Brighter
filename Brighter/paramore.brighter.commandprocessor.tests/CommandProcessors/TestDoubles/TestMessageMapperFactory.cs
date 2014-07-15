using System;
using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    class TestMessageMapperFactory : IAmAMessageMapperFactory
    {
        private readonly Func<IAmAMessageMapper> factoryMethod;

        public TestMessageMapperFactory(Func<IAmAMessageMapper> factoryMethod)
        {
            this.factoryMethod = factoryMethod;
        }

        public IAmAMessageMapper Create(Type messageMapperType)
        {
            return factoryMethod();
        }
    }
}
