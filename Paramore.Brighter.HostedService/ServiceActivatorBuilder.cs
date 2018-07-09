using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AspNetCore;

namespace Paramore.Brighter.HostedService
{
    public class ServiceActivatorBuilder : AspNetHandlerBuilder, IServiceActivatorBuilder
    {
        private readonly HostMessageMapperRegistry _mapperRegistry;

        public ServiceActivatorBuilder(IServiceCollection services, AspNetSubscriberRegistry subscriberRegistry, HostMessageMapperRegistry mapperRegistry) : base(services, subscriberRegistry)
        {
            _mapperRegistry = mapperRegistry;
        }

        public IServiceActivatorBuilder MapperRegistry(Action<HostMessageMapperRegistry> registerMappers)
        {
            if (registerMappers == null) throw new ArgumentNullException(nameof(registerMappers));
            
            registerMappers(_mapperRegistry);

            return this;
        }

        public IServiceActivatorBuilder MapperRegistryFromAssemblies(params Assembly[] assemblies)
        {
            if (assemblies.Length == 0)
                throw new ArgumentException("Value cannot be an empty collection.", nameof(assemblies));

            var mappers =
                from ti in assemblies.SelectMany(a => a.DefinedTypes)
                where ti.IsClass && !ti.IsAbstract && !ti.IsInterface
                from i in ti.ImplementedInterfaces
                where i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == typeof(IAmAMessageMapper<>)
                select new
                {
                    RequestType = i.GenericTypeArguments.First(),
                    HandlerType = ti.AsType()
                };

            foreach (var mapper in mappers)
            {
                _mapperRegistry.Add(mapper.RequestType, mapper.HandlerType);
            }

            return this;
        }
    }
}