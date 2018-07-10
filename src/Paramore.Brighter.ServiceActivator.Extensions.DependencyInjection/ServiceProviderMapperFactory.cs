using System;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public class ServiceProviderMapperFactory : IAmAMessageMapperFactory
    {
        private readonly IServiceCollection _serviceCollection;

        public ServiceProviderMapperFactory(IServiceCollection serviceCollection)
        {
            _serviceCollection = serviceCollection;
        }

        public IAmAMessageMapper Create(Type messageMapperType)
        {
            return (IAmAMessageMapper) _serviceCollection.BuildServiceProvider().GetService(messageMapperType);
        }
    }
}