using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class ServiceCollectionBrighterBuilder : IBrighterBuilder
    {
        private readonly ServiceCollectionSubscriberRegistry _serviceCollectionSubscriberRegistry;
        private readonly ServiceCollectionMessageMapperRegistry _mapperRegistry;

        /// <summary>
        /// Registers the components of Brighter pipelines
        /// </summary>
        /// <param name="services">The IoC container to update</param>
        /// <param name="serviceCollectionSubscriberRegistry">The register for looking up message handlers</param>
        /// <param name="mapperRegistry">The register for looking up message mappers</param>
        public ServiceCollectionBrighterBuilder(IServiceCollection services, ServiceCollectionSubscriberRegistry serviceCollectionSubscriberRegistry, ServiceCollectionMessageMapperRegistry mapperRegistry)
        {
            Services = services;
            _serviceCollectionSubscriberRegistry = serviceCollectionSubscriberRegistry;
            _mapperRegistry = mapperRegistry;
        }

        /// <summary>
        /// The IoC container we are populating
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Scan the assemblies provided for implementations of IHandleRequests, IHandleRequestsAsync, IAmAMessageMapper and register them with ServiceCollection
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns></returns>
        public IBrighterBuilder AutoFromAssemblies(params Assembly[] extraAssemblies)
        {
            var appDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !a.FullName.StartsWith("Microsoft.",true, CultureInfo.InvariantCulture) && !a.FullName.StartsWith("System.", true, CultureInfo.InvariantCulture));

            var assemblies = appDomainAssemblies.Concat(extraAssemblies).ToArray();

            MapperRegistryFromAssemblies(assemblies);
            HandlersFromAssemblies(assemblies);
            AsyncHandlersFromAssemblies(assemblies);

            return this;
        }

        /// <summary>
        /// Register message mappers
        /// </summary>
        /// <param name="registerMappers">A callback to register mappers</param>
        /// <returns></returns>
        public IBrighterBuilder MapperRegistry(Action<ServiceCollectionMessageMapperRegistry> registerMappers)
        {
            if (registerMappers == null) throw new ArgumentNullException(nameof(registerMappers));
            
            registerMappers(_mapperRegistry);

            return this;
        }

        /// <summary>
        /// Scan the assemblies provided for implementations of IAmAMessageMapper and register them with ServiceCollection
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>This builder, allows chaining calls</returns>
        public IBrighterBuilder MapperRegistryFromAssemblies(params Assembly[] assemblies)
        {
            if (assemblies.Length == 0)
                throw new ArgumentException("Value cannot be an empty collection.", nameof(assemblies));

            var mappers =
                from ti in assemblies.SelectMany(a => a.DefinedTypes).Distinct()
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

        /// <summary>
        /// Register handers with the built in subscriber registry
        /// </summary>
        /// <param name="registerHandlers">A callback to register handlers</param>
        /// <returns>This builder, allows chaining calls</returns>
        public IBrighterBuilder Handlers(Action<IAmASubscriberRegistry> registerHandlers)
        {
            if (registerHandlers == null)
                throw new ArgumentNullException(nameof(registerHandlers));

            registerHandlers(_serviceCollectionSubscriberRegistry);

            return this;
        }
        
        /// <summary>
        /// Scan the assemblies provided for implementations of IHandleRequests and register them with ServiceCollection
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>This builder, allows chaining calls</returns>
        public IBrighterBuilder HandlersFromAssemblies(params Assembly[] assemblies)
        {
            RegisterHandlersFromAssembly(typeof(IHandleRequests<>), assemblies, typeof(IHandleRequests<>).GetTypeInfo().Assembly);
            return this;
        }
        
        
        /// <summary>
        /// Scan the assemblies provided for implementations of IHandleRequestsAsyn and register them with ServiceCollection
        /// </summary>
        /// <param name="registerHandlers">A callback to register handlers</param>
        /// <returns>This builder, allows chaining calls</returns>
        public IBrighterBuilder AsyncHandlers(Action<IAmAnAsyncSubcriberRegistry> registerHandlers)
        {
            if (registerHandlers == null)
                throw new ArgumentNullException(nameof(registerHandlers));

            registerHandlers(_serviceCollectionSubscriberRegistry);

            return this;
        }

        /// <summary>
        /// Scan the assemblies provided for implementations of IHandleRequests and register them with ServiceCollection 
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>This builder, allows chaining calls</returns>
        public IBrighterBuilder AsyncHandlersFromAssemblies(params Assembly[] assemblies)
        {
            RegisterHandlersFromAssembly(typeof(IHandleRequestsAsync<>), assemblies, typeof(IHandleRequestsAsync<>).GetTypeInfo().Assembly);
            return this;
        }

        private void RegisterHandlersFromAssembly(Type interfaceType, IEnumerable<Assembly> assemblies, Assembly assembly)
        {
            assemblies = assemblies.Concat(new [] {assembly});
            var subscribers =
                from ti in assemblies.SelectMany(a => a.DefinedTypes).Distinct()
                where ti.IsClass && !ti.IsAbstract && !ti.IsInterface
                from i in ti.ImplementedInterfaces
                where i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == interfaceType
                select new
                {
                    RequestType = i.GenericTypeArguments.First(),
                    HandlerType = ti.AsType()
                };

            foreach (var subscriber in subscribers)
            {
                _serviceCollectionSubscriberRegistry.Add(subscriber.RequestType, subscriber.HandlerType);
            }
        }
    }
}
