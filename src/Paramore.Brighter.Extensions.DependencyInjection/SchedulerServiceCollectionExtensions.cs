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
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring request and message schedulers with the Brighter builder.
    /// </summary>
    public static class SchedulerServiceCollectionExtensions
    {
        /// <summary>
        /// Configures both request and message schedulers using a factory that implements both interfaces.
        /// </summary>
        /// <typeparam name="T">The scheduler factory type that implements both IAmAMessageSchedulerFactory and IAmARequestSchedulerFactory</typeparam>
        /// <param name="builder">The Brighter builder</param>
        /// <param name="factory">The scheduler factory instance</param>
        /// <returns>The builder for chaining</returns>
        public static IBrighterBuilder UseScheduler<T>(this IBrighterBuilder builder, T factory)
            where T : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
        {
            builder
                .UseRequestScheduler(factory)
                .UseMessageScheduler(factory);
            return builder;
        }

        /// <summary>
        /// Configures both request and message schedulers using a factory function that creates a type implementing both interfaces.
        /// </summary>
        /// <typeparam name="T">The scheduler factory type that implements both IAmAMessageSchedulerFactory and IAmARequestSchedulerFactory</typeparam>
        /// <param name="builder">The Brighter builder</param>
        /// <param name="factory">A function that takes IServiceProvider and returns the scheduler factory</param>
        /// <returns>The builder for chaining</returns>
        public static IBrighterBuilder UseScheduler<T>(this IBrighterBuilder builder, Func<IServiceProvider, T> factory)
            where T : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
        {
            builder
                .UseRequestScheduler(provider => factory(provider))
                .UseMessageScheduler(provider => factory(provider));
            return builder;
        }

        /// <summary>
        /// Configures a request scheduler factory for scheduling request execution.
        /// </summary>
        /// <param name="builder">The Brighter builder</param>
        /// <param name="factory">The request scheduler factory instance</param>
        /// <returns>The builder for chaining</returns>
        public static IBrighterBuilder UseRequestScheduler(this IBrighterBuilder builder, IAmARequestSchedulerFactory factory)
        {
            builder.Services.AddSingleton(factory);
            builder.Services.TryAddSingleton(provide =>
            {
                var command = provide.GetRequiredService<IAmACommandProcessor>();
                var schedulerfactory = provide.GetRequiredService<IAmARequestSchedulerFactory>();
                return schedulerfactory.CreateSync(command);
            });
            builder.Services.TryAddSingleton(provide =>
            {
                var command = provide.GetRequiredService<IAmACommandProcessor>();
                var schedulerFactory = provide.GetRequiredService<IAmARequestSchedulerFactory>();
                return schedulerFactory.CreateAsync(command);
            });
            return builder;
        }

        /// <summary>
        /// Configures a request scheduler factory using a factory function.
        /// </summary>
        /// <param name="builder">The Brighter builder</param>
        /// <param name="factory">A function that takes IServiceProvider and returns the request scheduler factory</param>
        /// <returns>The builder for chaining</returns>
        public static IBrighterBuilder UseRequestScheduler(this IBrighterBuilder builder, Func<IServiceProvider, IAmARequestSchedulerFactory> factory)
        {
            builder.Services.AddSingleton(factory);
            return builder;
        }

        /// <summary>
        /// Configures a message scheduler factory for scheduling message dispatch.
        /// </summary>
        /// <param name="builder">The Brighter builder</param>
        /// <param name="factory">The message scheduler factory instance</param>
        /// <returns>The builder for chaining</returns>
        public static IBrighterBuilder UseMessageScheduler(this IBrighterBuilder builder, IAmAMessageSchedulerFactory factory)
        {
            builder.Services.AddSingleton(factory);
            builder.Services.TryAddSingleton(provider =>
            {
                var messageSchedulerFactory = provider.GetRequiredService<IAmAMessageSchedulerFactory>();
                var processor = provider.GetRequiredService<IAmACommandProcessor>();
                return messageSchedulerFactory.Create(processor);
            });
            builder.Services.TryAddSingleton(provide => (IAmAMessageSchedulerAsync)provide.GetRequiredService<IAmAMessageScheduler>());
            builder.Services.TryAddSingleton(provide => (IAmAMessageSchedulerSync)provide.GetRequiredService<IAmAMessageScheduler>());
            return builder;
        }

        /// <summary>
        /// Configures a message scheduler factory using a factory function.
        /// </summary>
        /// <param name="builder">The Brighter builder</param>
        /// <param name="factory">A function that takes IServiceProvider and returns the message scheduler factory</param>
        /// <returns>The builder for chaining</returns>
        public static IBrighterBuilder UseMessageScheduler(this IBrighterBuilder builder, Func<IServiceProvider, IAmAMessageSchedulerFactory> factory)
        {
            builder.Services.AddSingleton(factory);
            return builder;
        }
    }
}
