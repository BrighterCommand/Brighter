using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Paramore.Brighter.AspNetCore
{
    internal class AspNetHandlerBuilder : IBrighterHandlerBuilder
    {
        private readonly AspNetSubscriberRegistry _subscriberRegistry;

        public AspNetHandlerBuilder(AspNetSubscriberRegistry subscriberRegistry)
        {
            _subscriberRegistry = subscriberRegistry;
        }

        public IBrighterHandlerBuilder Handlers(Action<IAmASubscriberRegistry> registerHandlers)
        {
            if (registerHandlers == null)
                throw new ArgumentNullException(nameof(registerHandlers));

            registerHandlers(_subscriberRegistry);

            return this;
        }

        public IBrighterHandlerBuilder HandlersFromAssemblies(params Assembly[] assemblies)
        {
            RegisterHandlersFromAssembly(typeof(IHandleRequests<>), assemblies);
            return this;
        }

        public IBrighterHandlerBuilder AsyncHandlers(Action<IAmAnAsyncSubcriberRegistry> registerHandlers)
        {
            if (registerHandlers == null)
                throw new ArgumentNullException(nameof(registerHandlers));

            registerHandlers(_subscriberRegistry);

            return this;
        }

        public IBrighterHandlerBuilder AsyncHandlersFromAssemblies(params Assembly[] assemblies)
        {
            RegisterHandlersFromAssembly(typeof(IHandleRequestsAsync<>), assemblies);
            return this;
        }

        private void RegisterHandlersFromAssembly(Type interfaceType, IEnumerable<Assembly> assemblies)
        {
            var subscribers =
                from t in assemblies.SelectMany(a => a.ExportedTypes)
                let ti = t.GetTypeInfo()
                where ti.IsClass && !ti.IsAbstract && !ti.IsInterface
                from i in t.GetTypeInfo().ImplementedInterfaces
                where i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == interfaceType
                select new
                {
                    RequestType = i.GenericTypeArguments.First(),
                    HandlerType = t
                };

            foreach (var subscriber in subscribers)
            {
                _subscriberRegistry.Add(subscriber.RequestType, subscriber.HandlerType);
            }
        }
    }
}