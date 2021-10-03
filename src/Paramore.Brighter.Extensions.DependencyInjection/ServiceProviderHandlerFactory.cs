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
        private readonly bool _isTransient;

        /// <summary>
        /// Constructs a factory that uses the .NET IoC container as the factory
        /// </summary>
        /// <param name="serviceProvider">The .NET IoC container</param>
        public ServiceProviderHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var options = (IBrighterOptions) serviceProvider.GetService(typeof(IBrighterOptions));
            if (options == null) _isTransient = true; else _isTransient = options.HandlerLifetime == ServiceLifetime.Transient;
        }

        /// <summary>
        /// Creates an instance of the request handler
        /// Lifetime is set during registration
        /// </summary>
        /// <param name="handlerType">The type of handler to request</param>
        /// <param name="lifetimeScope"></param>
        /// <returns>An instantiated request handler</returns>
        IHandleRequests IAmAHandlerFactorySync.Create(Type handlerType, IAmALifetime lifetimeScope)
        {
            return (IHandleRequests)_serviceProvider.GetService(handlerType);
        }

        /// <summary>
        /// Creates an instance of the request handler
        /// Lifetime is set during registration
        /// </summary>
        /// <param name="handlerType">The type of handler to request</param>
        /// <param name="lifetimeScope"></param>
        /// <returns>An instantiated request handler</returns>
        IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType, IAmALifetime lifetimeScope)
        {
            return (IHandleRequestsAsync)_serviceProvider.GetService(handlerType);
        }

        /// <summary>
        /// Release the request handler - actual behavior depends on lifetime, we only dispose if we are transient
        /// </summary>
        /// <param name="handler"></param>
        public void Release(IHandleRequests handler)
        {
            if (!_isTransient) return;
            
            var disposal = handler as IDisposable;
            disposal?.Dispose();
        }

        public void Release(IHandleRequestsAsync handler)
        {
            if (!_isTransient) return;
            
            var disposal = handler as IDisposable;
            disposal?.Dispose();
        }

        public IServiceScope CreateScope() => new NullScope();
    }
}
