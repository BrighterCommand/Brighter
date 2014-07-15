using System;
using paramore.brighter.commandprocessor;
using TinyIoC;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    class TinyIoCMessageMapperFactory : IAmAMessageMapperFactory
    {
        private readonly TinyIoCContainer container;

        public TinyIoCMessageMapperFactory(TinyIoCContainer container)
        {
            this.container = container;
        }

        public IAmAMessageMapper Create(Type messageMapperType)
        {
            return (IAmAMessageMapper) container.Resolve(messageMapperType);
        }
    }
}
