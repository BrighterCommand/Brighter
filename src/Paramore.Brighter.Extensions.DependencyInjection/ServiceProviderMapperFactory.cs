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
    /// Creates a message mapper from the underlying .NET IoC container.
    /// Supports singleton, scoped, and transient lifetimes based on <see cref="IBrighterOptions.MapperLifetime"/>.
    /// </summary>
    public class ServiceProviderMapperFactory : IAmAMessageMapperFactory, IDisposable
    {
        private readonly ServiceProviderLifetimeScope _lifetimeScope;

        /// <summary>
        /// Constructs a mapper factory that uses the .NET Service Provider for implementation details
        /// </summary>
        /// <param name="serviceProvider">The .NET IoC container</param>
        public ServiceProviderMapperFactory(IServiceProvider serviceProvider)
        {
            var options = (IBrighterOptions?)serviceProvider.GetService(typeof(IBrighterOptions));
            var lifetime = options?.MapperLifetime ?? ServiceLifetime.Singleton;
            _lifetimeScope = new ServiceProviderLifetimeScope(serviceProvider, lifetime);
        }

        /// <summary>
        /// Create an instance of the message mapper type from the .NET IoC container.
        /// Lifetime is determined by <see cref="IBrighterOptions.MapperLifetime"/>.
        /// </summary>
        /// <param name="messageMapperType">The type of mapper to instantiate</param>
        /// <returns>The created mapper instance</returns>
        public IAmAMessageMapper? Create(Type messageMapperType)
        {
            return _lifetimeScope.GetOrCreate<IAmAMessageMapper>(messageMapperType);
        }

        /// <summary>
        /// Disposes of the factory and its lifetime scope.
        /// </summary>
        public void Dispose()
        {
            _lifetimeScope.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
