﻿#region Licence

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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class ServiceCollectionBrighterBuilder : IBrighterBuilder
    {
        private readonly ServiceCollectionSubscriberRegistry _serviceCollectionSubscriberRegistry;
        private readonly ServiceCollectionMessageMapperRegistry _mapperRegistry;
        private readonly ServiceCollectionTransformerRegistry _transformerRegistry;

        /// <summary>
        /// Registers the components of Brighter pipelines
        /// </summary>
        /// <param name="services">The IoC container to update</param>
        /// <param name="serviceCollectionSubscriberRegistry">The register for looking up message handlers</param>
        /// <param name="mapperRegistry">The register for looking up message mappers</param>
        /// <param name="transformerRegistry">The register for transforms</param>
        public ServiceCollectionBrighterBuilder(
            IServiceCollection services, 
            ServiceCollectionSubscriberRegistry serviceCollectionSubscriberRegistry,
            ServiceCollectionMessageMapperRegistry mapperRegistry, 
            ServiceCollectionTransformerRegistry transformerRegistry = null)
        {
            Services = services;
            _serviceCollectionSubscriberRegistry = serviceCollectionSubscriberRegistry;
            _mapperRegistry = mapperRegistry;
            _transformerRegistry = transformerRegistry ?? new ServiceCollectionTransformerRegistry(services);
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
            var appDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a =>
                !a.IsDynamic && !a.FullName.StartsWith("Microsoft.", true, CultureInfo.InvariantCulture) &&
                !a.FullName.StartsWith("System.", true, CultureInfo.InvariantCulture));

            var assemblies = appDomainAssemblies.Concat(extraAssemblies).ToArray();

            MapperRegistryFromAssemblies(assemblies);
            HandlersFromAssemblies(assemblies);
            AsyncHandlersFromAssemblies(assemblies);
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
                where i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAmAMessageMapper<>)
                select new { RequestType = i.GenericTypeArguments.First(), HandlerType = ti.AsType() };

            foreach (var mapper in mappers)
            {
                _mapperRegistry.Add(mapper.RequestType, mapper.HandlerType);
            }

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
        /// <returns>This builder, allows chaining calls</returns>
        public IBrighterBuilder HandlersFromAssemblies(params Assembly[] assemblies)
        {
            RegisterHandlersFromAssembly(typeof(IHandleRequests<>), assemblies, typeof(IHandleRequests<>).Assembly);
            return this;
        }


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
        /// <returns>This builder, allows chaining calls</returns>
        public IBrighterBuilder AsyncHandlersFromAssemblies(params Assembly[] assemblies)
        {
            RegisterHandlersFromAssembly(typeof(IHandleRequestsAsync<>), assemblies, typeof(IHandleRequestsAsync<>).Assembly);
            return this;
        }


        /// <summary>
        /// Scan the assemblies for implementations of IAmAMessageTransformAsync and register them with the ServiceCollection
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>This builder, allows chaining calls</returns>
        /// <exception cref="ArgumentException">Thrown if there are no assemblies passed to the method</exception>
        public IBrighterBuilder TransformsFromAssemblies(params Assembly[] assemblies)
        {
            if (assemblies.Length == 0)
                throw new ArgumentException("Value cannot be an empty collection.", nameof(assemblies));

            var transforms =
                from ti in assemblies.SelectMany(a => a.DefinedTypes).Distinct()
                where ti.IsClass && !ti.IsAbstract && !ti.IsInterface
                from i in ti.ImplementedInterfaces
                where i == typeof(IAmAMessageTransformAsync)
                select new { TransformType = ti.AsType() };

            foreach (var transform in transforms)
            {
                _transformerRegistry.Add(transform.TransformType);
            }

            return this;
        }

        private void RegisterHandlersFromAssembly(Type interfaceType, IEnumerable<Assembly> assemblies, Assembly assembly)
        {
            assemblies = assemblies.Concat(new[] { assembly });
            var subscribers =
                from ti in assemblies.SelectMany(a => a.DefinedTypes).Distinct()
                where ti.IsClass && !ti.IsAbstract && !ti.IsInterface
                from i in ti.ImplementedInterfaces
                where i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType
                select new { RequestType = i.GenericTypeArguments.First(), HandlerType = ti.AsType() };

            foreach (var subscriber in subscribers)
            {
                _serviceCollectionSubscriberRegistry.Add(subscriber.RequestType, subscriber.HandlerType);
            }
        }
    }
}
