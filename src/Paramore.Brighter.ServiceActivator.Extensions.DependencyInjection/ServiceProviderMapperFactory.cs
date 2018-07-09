using System;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public class ServiceProviderMapperFactory : IAmAMessageMapperFactory
    {
        private readonly IServiceProvider _container;

        public ServiceProviderMapperFactory(IServiceProvider serviceProvider)
        {
            _container = serviceProvider;
        }

        public IAmAMessageMapper Create(Type messageMapperType)
        {
            return (IAmAMessageMapper) _container.GetService(messageMapperType);
        }
    }
}