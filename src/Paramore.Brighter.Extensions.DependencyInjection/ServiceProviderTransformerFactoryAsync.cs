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
    /// A factory for creating async transformers, backed by the .NET Service Collection.
    /// Supports singleton, scoped, and transient lifetimes based on <see cref="IBrighterOptions.TransformerLifetime"/>.
    /// </summary>
    public class ServiceProviderTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync, IDisposable
    {
        private readonly ServiceProviderLifetimeScope _lifetimeScope;

        /// <summary>
        /// Constructs a transformer factory
        /// </summary>
        /// <param name="serviceProvider">The IoC container we use to satisfy requests for transforms</param>
        public ServiceProviderTransformerFactoryAsync(IServiceProvider serviceProvider)
        {
            var options = (IBrighterOptions?)serviceProvider.GetService(typeof(IBrighterOptions));
            var lifetime = options?.TransformerLifetime ?? ServiceLifetime.Singleton;
            _lifetimeScope = new ServiceProviderLifetimeScope(serviceProvider, lifetime);
        }

        /// <summary>
        /// Creates a specific transformer on demand.
        /// Lifetime is determined by <see cref="IBrighterOptions.TransformerLifetime"/>.
        /// </summary>
        /// <param name="transformerType">The type of transformer to create</param>
        /// <returns>The created transformer instance</returns>
        public IAmAMessageTransformAsync? Create(Type transformerType)
        {
            return _lifetimeScope.GetOrCreate<IAmAMessageTransformAsync>(transformerType);
        }

        /// <summary>
        /// Releases a transformer. For singleton lifetime, does nothing.
        /// For scoped/transient, disposes the transformer if it implements IDisposable.
        /// </summary>
        /// <param name="transformer">The transformer to release</param>
        public void Release(IAmAMessageTransformAsync transformer)
        {
            _lifetimeScope.Release(transformer);
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
