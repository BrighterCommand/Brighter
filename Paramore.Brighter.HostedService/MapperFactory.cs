using System;

namespace Paramore.Brighter.HostedService
{
    public class MapperFactory : IAmAMessageMapperFactory
    {
        private readonly IServiceProvider _container;

        public MapperFactory(IServiceProvider serviceProvider)
        {
            _container = serviceProvider;
        }

        public IAmAMessageMapper Create(Type messageMapperType)
        {
            return (IAmAMessageMapper) _container.GetService(messageMapperType);
        }
    }
}