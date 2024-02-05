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

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// A factory for creating transformers, backed by the .NET Service Collection
    /// </summary>
    public class ServiceProviderTransformerFactory : IAmAMessageTransformerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly bool _isTransient;

        /// <summary>
        /// Constructs a transformer factory
        /// </summary>
        /// <param name="serviceProvider">The IoC container we use to satisfy requests for transforms</param>
        public ServiceProviderTransformerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var options = serviceProvider.GetRequiredService<IBrighterOptions>();
            if (options == null) _isTransient = false; else _isTransient = options.HandlerLifetime == ServiceLifetime.Transient;  
        }
    
        /// <summary>
        /// Creates a specific transformer on demand
        /// </summary>
        /// <param name="transformerType">The type of transformer to create</param>
        /// <returns></returns>
        public IAmAMessageTransform Create(Type transformerType)
        {
            return (IAmAMessageTransform) _serviceProvider.GetService(transformerType);
        }

        /// <summary>
        /// If the transform was scoped as transient, we release it when the pipeline is finished
        /// </summary>
        /// <param name="transformer"></param>
        public void Release(IAmAMessageTransform transformer)
        {
            if (!_isTransient) return;
            
            var disposal = transformer as IDisposable;
            disposal?.Dispose();
        }
    }
}
