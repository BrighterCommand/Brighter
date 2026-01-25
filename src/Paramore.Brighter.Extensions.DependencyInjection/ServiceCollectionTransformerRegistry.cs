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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// This class helps us register transformers with the IoC container
    /// We don't have a separate registry for transformers, but we do need to understand
    /// the service lifetime options for the transformers which we want to register
    /// </summary>
    public class ServiceCollectionTransformerRegistry : ITransformerRegistry
    {
        private readonly IServiceCollection _services;

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="services">The Service Collection to register the transforms with</param>
        public ServiceCollectionTransformerRegistry(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Register a transform with the IServiceCollection using Transient lifetime
        /// </summary>
        /// <param name="transform">The type of the transform to register</param>
        public void Add(Type transform)
        {
            _services.TryAdd(new ServiceDescriptor(transform, transform, ServiceLifetime.Transient));
        }
    }
}
