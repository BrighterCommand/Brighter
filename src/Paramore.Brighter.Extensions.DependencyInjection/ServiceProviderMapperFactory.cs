using System;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class ServiceProviderMapperFactory : IAmAMessageMapperFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceProviderMapperFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IAmAMessageMapper Create(Type messageMapperType)
        {
            return (IAmAMessageMapper) _serviceProvider.GetService(messageMapperType);
        }
    }
}