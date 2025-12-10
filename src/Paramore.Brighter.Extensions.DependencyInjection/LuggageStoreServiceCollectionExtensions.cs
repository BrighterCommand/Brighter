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
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring external luggage (claim check) stores with the Brighter builder.
    /// </summary>
    public static class LuggageStoreServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a singleton instance of an external luggage (claim check) store provider to the Brighter framework.
        /// The store provider type will be resolved from the service provider.
        /// The store provider must implement both <see cref="IAmAStorageProvider"/> for synchronous operations
        /// and <see cref="IAmAStorageProviderAsync"/> for asynchronous operations.
        /// </summary>
        /// <typeparam name="TStoreProvider">The concrete type of the storage provider.
        /// Must implement <see cref="IAmAStorageProvider"/> and <see cref="IAmAStorageProviderAsync"/>.</typeparam>
        /// <param name="builder">The <see cref="IBrighterBuilder"/> instance to which the storage provider will be added.</param>
        /// <returns>The <see cref="IBrighterBuilder"/> instance for chaining.</returns>
        public static IBrighterBuilder UseExternalLuggageStore<TStoreProvider>(this IBrighterBuilder builder)
            where TStoreProvider : class, IAmAStorageProvider, IAmAStorageProviderAsync
        {
            builder.Services.AddSingleton<TStoreProvider>()
                .RegisterLuggageStore<TStoreProvider>();

            return builder;
        }

        /// <summary>
        /// Adds a singleton instance of an external luggage (claim check) store provider to the Brighter framework.
        /// This method is used when you have a pre-initialized instance of your storage provider.
        /// The store provider must implement both <see cref="IAmAStorageProvider"/> for synchronous operations
        /// and <see cref="IAmAStorageProviderAsync"/> for asynchronous operations.
        /// </summary>
        /// <typeparam name="TStoreProvider">The concrete type of the storage provider.
        /// Must implement <see cref="IAmAStorageProvider"/> and <see cref="IAmAStorageProviderAsync"/>.</typeparam>
        /// <param name="builder">The <see cref="IBrighterBuilder"/> instance to which the storage provider will be added.</param>
        /// <param name="storeProvider">The pre-initialized instance of the storage provider.</param>
        /// <returns>The <see cref="IBrighterBuilder"/> instance for chaining.</returns>
        public static IBrighterBuilder UseExternalLuggageStore<TStoreProvider>(this IBrighterBuilder builder, TStoreProvider storeProvider)
            where TStoreProvider : class, IAmAStorageProvider, IAmAStorageProviderAsync
        {
            builder.Services.AddSingleton(storeProvider)
                 .RegisterLuggageStore<TStoreProvider>();

            return builder;
        }

        /// <summary>
        /// Adds a singleton instance of a luggage (claim check) store provider to the Brighter framework,
        /// resolved via a factory function. This method is used when the storage provider
        /// needs to be instantiated by the service provider (e.g., to inject its own dependencies).
        /// The store provider must implement both <see cref="IAmAStorageProvider"/> for synchronous operations
        /// and <see cref="IAmAStorageProviderAsync"/> for asynchronous operations.
        /// </summary>
        /// <typeparam name="TStoreProvider">The concrete type of the storage provider.
        /// Must implement <see cref="IAmAStorageProvider"/> and <see cref="IAmAStorageProviderAsync"/>.</typeparam>
        /// <param name="builder">The <see cref="IBrighterBuilder"/> instance to which the storage provider will be added.</param>
        /// <param name="storeProvider">A factory function that takes an <see cref="IServiceProvider"/> and returns an instance of the storage provider.</param>
        /// <returns>The <see cref="IBrighterBuilder"/> instance for chaining.</returns>
        public static IBrighterBuilder UseExternalLuggageStore<TStoreProvider>(this IBrighterBuilder builder, Func<IServiceProvider, TStoreProvider> storeProvider)
            where TStoreProvider : class, IAmAStorageProvider, IAmAStorageProviderAsync
        {
            builder.Services.AddSingleton(storeProvider)
                .RegisterLuggageStore<TStoreProvider>();

            return builder;
        }

        private static void RegisterLuggageStore<TStoreProvider>(this IServiceCollection services)
            where TStoreProvider : class, IAmAStorageProvider, IAmAStorageProviderAsync
        {
            services
                .AddSingleton(provider =>
                {
                    IAmAStorageProvider store = provider.GetRequiredService<TStoreProvider>();
                    store.Tracer = provider.GetRequiredService<IAmABrighterTracer>();
                    store.EnsureStoreExists();
                    return store;
                })
                .AddSingleton(provider =>
                {
                    IAmAStorageProviderAsync store = provider.GetRequiredService<TStoreProvider>();
                    store.Tracer = provider.GetRequiredService<IAmABrighterTracer>();
                    store.EnsureStoreExistsAsync().GetAwaiter().GetResult();
                    return store;
                });
        }
    }
}
