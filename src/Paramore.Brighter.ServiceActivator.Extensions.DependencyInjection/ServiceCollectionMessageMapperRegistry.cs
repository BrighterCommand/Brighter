using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public class ServiceCollectionMessageMapperRegistry 
    {
        private readonly IServiceCollection _serviceCollection;
        private readonly ServiceLifetime _serviceLifetime;
        private readonly Dictionary<Type, Type> _mapperRegistry = new Dictionary<Type, Type>();

        public ServiceCollectionMessageMapperRegistry(IServiceCollection serviceCollection, ServiceLifetime serviceLifetime)
        {
            _serviceCollection = serviceCollection;
            _serviceLifetime = serviceLifetime;
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

        public Dictionary<Type, Type> GetAll()
        {
            return _mapperRegistry;
        }
    }
}