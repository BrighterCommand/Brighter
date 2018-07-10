using System;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public class ServiceCollectionMessageMapperRegistry : IAmAMessageMapperRegistry  
    {
        private readonly IServiceCollection _serviceCollection;
        private readonly ServiceLifetime _serviceLifetime;
        private readonly MessageMapperRegistry _mapperRegistry;

        public ServiceCollectionMessageMapperRegistry(
            IServiceCollection serviceCollection,
            ServiceLifetime serviceLifetime)
        {
            _serviceCollection = serviceCollection;
            _serviceLifetime = serviceLifetime;
            _mapperRegistry = new MessageMapperRegistry(new ServiceProviderMapperFactory(serviceCollection));

           
        }
        

        public IAmAMessageMapper<T> Get<T>() where T : class, IRequest
        {
            return _mapperRegistry.Get<T>();
        }

        public void Register<TRequest, TMessageMapper>() where TRequest : class, IRequest where TMessageMapper : class, IAmAMessageMapper<TRequest>
        {
            Add(typeof(TRequest), typeof(TMessageMapper));
        }
        
        public void Add(Type message, Type mapper)
        {
            _serviceCollection.Add(new ServiceDescriptor(mapper, mapper, _serviceLifetime));
            _mapperRegistry.Add(message, mapper);
        }
    }
}