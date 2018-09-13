using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class ServiceCollectionBrighterBuilder : IBrighterHandlerBuilder
    {
        private readonly ServiceCollectionSubscriberRegistry _serviceCollectionSubscriberRegistry;

        public ServiceCollectionBrighterBuilder(IServiceCollection services, ServiceCollectionSubscriberRegistry serviceCollectionSubscriberRegistry)
        {
            Services = services;
            _serviceCollectionSubscriberRegistry = serviceCollectionSubscriberRegistry;
        }

        public IServiceCollection Services { get; }

        public IBrighterHandlerBuilder Handlers(Action<IAmASubscriberRegistry> registerHandlers)
        {
            if (registerHandlers == null)
                throw new ArgumentNullException(nameof(registerHandlers));

            registerHandlers(_serviceCollectionSubscriberRegistry);

            return this;
        }

        public IBrighterHandlerBuilder HandlersFromAssemblies(params Assembly[] assemblies)
        {
            RegisterHandlersFromAssembly(typeof(IHandleRequests<>), assemblies, typeof(IHandleRequests<>).GetTypeInfo().Assembly);
            return this;
        }

        public IBrighterHandlerBuilder AsyncHandlers(Action<IAmAnAsyncSubcriberRegistry> registerHandlers)
        {
            if (registerHandlers == null)
                throw new ArgumentNullException(nameof(registerHandlers));

            registerHandlers(_serviceCollectionSubscriberRegistry);

            return this;
        }

        public IBrighterHandlerBuilder AsyncHandlersFromAssemblies(params Assembly[] assemblies)
        {
            RegisterHandlersFromAssembly(typeof(IHandleRequestsAsync<>), assemblies, typeof(IHandleRequestsAsync<>).GetTypeInfo().Assembly);
            return this;
        }

        private void RegisterHandlersFromAssembly(Type interfaceType, IEnumerable<Assembly> assemblies,
            Assembly assembly)
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