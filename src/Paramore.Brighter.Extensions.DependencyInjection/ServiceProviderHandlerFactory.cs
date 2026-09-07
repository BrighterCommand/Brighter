#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// A factory for handlers using the .NET IoC container for implementation details
    /// </summary>
    public class ServiceProviderHandlerFactory : IAmAHandlerFactorySync, IAmAHandlerFactoryAsync
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceLifetime _handlerLifetime;
        private readonly bool _isolateTransientHandlerScope;
        private readonly ServiceProviderLifetimeScope _singletonScope;
        private readonly IAmAScopeProvider? _scopeProvider;
        private readonly ScopeAffinityPolicy _scopeAffinityPolicy;

        /// <summary>
        /// Constructs a factory that uses the .NET IoC container as the factory
        /// </summary>
        /// <param name="serviceProvider">The .NET IoC container</param>
        public ServiceProviderHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var options = (IBrighterOptions?)serviceProvider.GetService(typeof(IBrighterOptions));
            _handlerLifetime = options?.HandlerLifetime ?? ServiceLifetime.Transient;
            _isolateTransientHandlerScope = options?.IsolateTransientHandlerScope ?? true;
            _singletonScope = new ServiceProviderLifetimeScope(serviceProvider, ServiceLifetime.Singleton);
            _scopeProvider = (IAmAScopeProvider?)serviceProvider.GetService(typeof(IAmAScopeProvider));
            _scopeAffinityPolicy = new ScopeAffinityPolicy(options);
        }

        /// <summary>
        /// Offers a new per-pipeline DI scope for a <c>Scoped</c> or <c>Transient</c> handler lifetime, so
        /// every handler in one pipeline resolves from one scope rather than a factory-wide one. Offers
        /// none for <c>Singleton</c>, which resolves from the container-wide singleton scope instead.
        /// </summary>
        /// <remarks>
        /// Unlike the mapper/transformer factories, <c>Transient</c> also gets a handle here: a per-request
        /// pipeline scope is how a <c>Transient</c> handler's own per-resolution isolation is delivered
        /// (see <see cref="ServiceProviderLifetimeScope"/>'s isolated-transient-scope support), not an
        /// optional convenience. It does not, however, ask for an ambient: only a <c>Scoped</c> handler
        /// pipeline ever asks.
        /// </remarks>
        /// <exception cref="AmbientScopeSourceException">
        /// A registered <see cref="IAmAScopeProvider"/>'s <c>GetAmbient</c> threw. The calling pipeline
        /// builder recognises this type and rethrows the inner exception unwrapped.
        /// </exception>
        public IAmAScope? CreatePipelineScope()
        {
            if (_handlerLifetime == ServiceLifetime.Scoped)
            {
                var borrowed = AmbientScopeQuery.Ask(_scopeProvider, _scopeAffinityPolicy.ForHandlerPipeline(), _serviceProvider);
                if (borrowed is not null) return borrowed;
            }

            return _handlerLifetime == ServiceLifetime.Singleton
                ? null
                : new ServiceProviderPipelineScope(new ServiceProviderLifetimeScope(_serviceProvider, _handlerLifetime, _isolateTransientHandlerScope));
        }

        /// <summary>
        /// Creates an instance of the request handler
        /// Lifetime is set during registration
        /// </summary>
        /// <param name="handlerType">The type of handler to request</param>
        /// <param name="lifetime">The brighter Handler lifetime</param>
        /// <returns>An instantiated request handler</returns>
        IHandleRequests? IAmAHandlerFactorySync.Create(Type handlerType, IAmALifetime lifetime)
        {
            if (_handlerLifetime == ServiceLifetime.Singleton)
                return _singletonScope.GetOrCreate<IHandleRequests>(handlerType);

            return ResolvePipelineScope(lifetime).GetOrCreate<IHandleRequests>(handlerType);
        }

        /// <summary>
        /// Creates an instance of the request handler
        /// Lifetime is set during registration
        /// </summary>
        /// <param name="handlerType">The type of handler to request</param>
        /// <param name="lifetime">The brighter Handler lifetime</param>
        /// <returns>An instantiated request handler</returns>
        IHandleRequestsAsync? IAmAHandlerFactoryAsync.Create(Type handlerType, IAmALifetime lifetime)
        {
            if (_handlerLifetime == ServiceLifetime.Singleton)
                return _singletonScope.GetOrCreate<IHandleRequestsAsync>(handlerType);

            return ResolvePipelineScope(lifetime).GetOrCreate<IHandleRequestsAsync>(handlerType);
        }

        /// <summary>
        /// Resolves the <see cref="ServiceProviderLifetimeScope"/> backing the pipeline scope handle a
        /// <c>Scoped</c> or <c>Transient</c> handler must have been supplied via <see cref="CreatePipelineScope"/>.
        /// </summary>
        /// <exception cref="ConfigurationException">
        /// Thrown when <paramref name="lifetime"/> (or its <see cref="IAmALifetime.PipelineScope"/>) carries
        /// no handle this factory recognises — the caller did not pass this factory's own
        /// <see cref="CreatePipelineScope"/> result through to the <see cref="IAmALifetime"/> it created.
        /// </exception>
        private static ServiceProviderLifetimeScope ResolvePipelineScope(IAmALifetime lifetime)
        {
            if (lifetime?.PipelineScope is ServiceProviderPipelineScope pipelineScope)
                return pipelineScope.LifetimeScope;

            throw new ConfigurationException(
                "No pipeline scope was supplied for a Scoped or Transient handler lifetime. Pass this " +
                "factory's own CreatePipelineScope() result through to the IAmALifetime constructed for " +
                "the pipeline.");
        }

        /// <summary>
        /// Release the request handler.
        /// </summary>
        /// <remarks>
        /// A no-op: whichever <see cref="ServiceProviderLifetimeScope"/> the handler was resolved from —
        /// the container-wide singleton scope, or the per-pipeline scope offered via
        /// <see cref="CreatePipelineScope"/> — disposes every handler it resolved when that scope itself is
        /// disposed. <see cref="HandlerLifetimeScope"/> disposes the pipeline scope handle once, after
        /// releasing every tracked handler, so disposing the handler here as well would dispose it twice.
        /// </remarks>
        /// <param name="handler"></param>
        /// <param name="lifetime">The brighter Handler lifetime</param>
        public void Release(IHandleRequests handler, IAmALifetime lifetime)
        {
        }

        /// <summary>
        /// Release the request handler.
        /// </summary>
        /// <remarks>
        /// A no-op: whichever <see cref="ServiceProviderLifetimeScope"/> the handler was resolved from —
        /// the container-wide singleton scope, or the per-pipeline scope offered via
        /// <see cref="CreatePipelineScope"/> — disposes every handler it resolved when that scope itself is
        /// disposed. <see cref="HandlerLifetimeScope"/> disposes the pipeline scope handle once, after
        /// releasing every tracked handler, so disposing the handler here as well would dispose it twice.
        /// </remarks>
        /// <param name="handler"></param>
        /// <param name="lifetime">The brighter Handler lifetime</param>
        public void Release(IHandleRequestsAsync? handler, IAmALifetime lifetime)
        {
        }
    }
}
