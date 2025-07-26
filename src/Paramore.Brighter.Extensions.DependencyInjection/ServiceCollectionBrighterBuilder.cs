#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class ServiceCollectionBrighterBuilder : IBrighterBuilder
    {
        private readonly ServiceCollectionSubscriberRegistry _serviceCollectionSubscriberRegistry;
        private readonly ServiceCollectionMessageMapperRegistry _mapperRegistry;
        private readonly ServiceCollectionTransformerRegistry _transformerRegistry;
        
        public IPolicyRegistry<string>? PolicyRegistry { get; set; }

        /// <summary>
        /// Registers the components of Brighter pipelines
        /// </summary>
        /// <param name="services">The IoC container to update</param>
        /// <param name="serviceCollectionSubscriberRegistry">The register for looking up message handlers</param>
        /// <param name="mapperRegistry">The register for looking up message mappers</param>
        /// <param name="transformerRegistry">The register for transforms</param>
        /// <param name="policyRegistry">The list of policies that we require</param>
        public ServiceCollectionBrighterBuilder(
            IServiceCollection services,
            ServiceCollectionSubscriberRegistry serviceCollectionSubscriberRegistry,
            ServiceCollectionMessageMapperRegistry mapperRegistry,
            ServiceCollectionTransformerRegistry? transformerRegistry = null,
            IPolicyRegistry<string>? policyRegistry = null
            )
        {
            Services = services;
            _serviceCollectionSubscriberRegistry = serviceCollectionSubscriberRegistry;
            _mapperRegistry = mapperRegistry;
            _transformerRegistry = transformerRegistry ?? new ServiceCollectionTransformerRegistry(services);
            PolicyRegistry = policyRegistry;
        }

        /// <summary>
        /// The IoC container we are populating
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Scan the assemblies provided for implementations of IHandleRequestsAsync and register them with ServiceCollection
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
        /// <param name="excludeDynamicHandlerTypes">If you want to register a handler with a dynamic routing rule - an agreement - you need to excluce it from auto-regisration by adding it to this list</param>
        /// <returns>This builder, allows chaining calls</returns>
        public IBrighterBuilder AsyncHandlersFromAssemblies(IEnumerable<Assembly> assemblies, IEnumerable<Type>? excludeDynamicHandlerTypes)
        {
            RegisterHandlersFromAssembly(typeof(IHandleRequestsAsync<>), assemblies, typeof(IHandleRequestsAsync<>).Assembly, excludeDynamicHandlerTypes);
            return this;
        }

        /// <summary>
        /// Scan the assemblies provided for implementations of IHandleRequests, IHandleRequestsAsync, IAmAMessageMapper and register them with ServiceCollection
        /// </summary>
        /// <param name="extraAssemblies">Additional assemblies to scan</param>
        /// <param name="excludeDynamicHandlerTypes">If you want to register a handler with a dynamic routing rule - an agreement - you need to excluce it from auto-regisration by adding it to this list</param>
        /// <returns></returns>
        public IBrighterBuilder AutoFromAssemblies(IEnumerable<Assembly>? extraAssemblies = null, IEnumerable<Type>? excludeDynamicHandlerTypes = null)
        {
            var appDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a =>
                !a.IsDynamic && a.FullName?.StartsWith("Microsoft.", true, CultureInfo.InvariantCulture) != true &&
                a.FullName?.StartsWith("System.", true, CultureInfo.InvariantCulture) != true);

            var assemblies = extraAssemblies !=  null ? appDomainAssemblies.Concat(extraAssemblies) : appDomainAssemblies;

            MapperRegistryFromAssemblies(assemblies);
            HandlersFromAssemblies(assemblies, excludeDynamicHandlerTypes);
            AsyncHandlersFromAssemblies(assemblies, excludeDynamicHandlerTypes);
            TransformsFromAssemblies(assemblies);

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
            _mapperRegistry.EnsureDefaultMessageMapperIsRegistered();

            return this;
        }

        /// <summary>
        /// Scan the assemblies provided for implementations of IAmAMessageMapper and register them with ServiceCollection
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>This builder, allows chaining calls</returns>
        public IBrighterBuilder MapperRegistryFromAssemblies(IEnumerable<Assembly> assemblies)
        {
            if (!assemblies.Any())
                throw new ArgumentException("Value cannot be an empty collection.", nameof(assemblies));

            RegisterMappersFromAssemblies(assemblies);
            RegisterAsyncMappersFromAssemblies(assemblies);

            return this;
        }


        /// <summary>
        /// Register handlers with the built in subscriber registry
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
        /// <param name="excludeDynamicHandlerTypes">If you want to register a handler with a dynamic routing rule - an agreement - you need to excluce it from auto-regisration by adding it to this list</param>
        /// <returns>This builder, allows chaining calls</returns>
        public IBrighterBuilder HandlersFromAssemblies(IEnumerable<Assembly> assemblies, IEnumerable<Type>? excludeDynamicHandlerTypes)
        {
            RegisterHandlersFromAssembly(typeof(IHandleRequests<>), assemblies, typeof(IHandleRequests<>).Assembly, excludeDynamicHandlerTypes);
            return this;
        }

        /// <summary>
        /// Scan the assemblies for implementations of IAmAMessageTransformAsync and register them with the ServiceCollection
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>This builder, allows chaining calls</returns>
        /// <exception cref="ArgumentException">Thrown if there are no assemblies passed to the method</exception>
        public IBrighterBuilder TransformsFromAssemblies(IEnumerable<Assembly> assemblies)
        {
            if (!assemblies.Any())
                throw new ArgumentException("Value cannot be an empty collection.", nameof(assemblies));

            var transforms =
                from ti in assemblies.SelectMany(GetLoadableTypes).Distinct()
                where ti is { IsClass: true, IsAbstract: false, IsInterface: false }
                from i in ti.GetInterfaces()
                where typeof(IAmAMessageTransformAsync).IsAssignableFrom(i) || typeof(IAmAMessageTransform).IsAssignableFrom(i)
                select new { TransformType = ti };

            foreach (var transform in transforms)
            {
                _transformerRegistry.Add(transform.TransformType);
            }

            return this;
        }
        
        private void RegisterHandlersFromAssembly(Type interfaceType, IEnumerable<Assembly> assemblies, Assembly assembly, IEnumerable<Type>? excludeDynamicHandlerTypes)
        {
            assemblies = assemblies.Concat([assembly]);
            var subscribers =
                from ti in assemblies.SelectMany(GetLoadableTypes).Distinct()
                where ti is { IsClass: true, IsAbstract: false, IsInterface: false }
                from i in ti.GetInterfaces()
                where i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType
                select new { RequestType = i.GenericTypeArguments.First(), HandlerType = ti };

            foreach (var subscriber in subscribers)
            {
                if (excludeDynamicHandlerTypes != null && excludeDynamicHandlerTypes.Contains(subscriber.HandlerType))
                    continue; // Skip dynamic handlers
                
                _serviceCollectionSubscriberRegistry.Add(subscriber.RequestType, subscriber.HandlerType);
            }
        }
        
        private void RegisterMappersFromAssemblies(IEnumerable<Assembly> assemblies)
        {
            var mappers =
                from ti in assemblies.SelectMany(GetLoadableTypes).Distinct()
                where ti is { IsClass: true, IsAbstract: false, IsInterface: false }
                from i in ti.GetInterfaces()
                where i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAmAMessageMapper<>)
                select new { RequestType = i.GenericTypeArguments.First(), HandlerType = ti };

            foreach (var mapper in mappers)
            {
                _mapperRegistry.Add(mapper.RequestType, mapper.HandlerType);
            }
        }
        
        private void RegisterAsyncMappersFromAssemblies(IEnumerable<Assembly> assemblies)
        {
            var mappers =
                from ti in assemblies.SelectMany(GetLoadableTypes).Distinct()
                where ti is { IsClass: true, IsAbstract: false, IsInterface: false }
                from i in ti.GetInterfaces()
                where i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAmAMessageMapperAsync<>)
                select new { RequestType = i.GenericTypeArguments.First(), HandlerType = ti };

            foreach (var mapper in mappers)
            {
                _mapperRegistry.AddAsync(mapper.RequestType, mapper.HandlerType);
            }
        }

        private static Type?[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types;
            }
        }
    }
}
