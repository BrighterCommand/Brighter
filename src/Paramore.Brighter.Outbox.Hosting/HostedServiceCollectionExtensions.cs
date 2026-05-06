#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.Hosting
{
    public static class HostedServiceCollectionExtensions
    {
        /// <summary>
        /// Use a timer based outbox sweeper as a Hosted Service.
        /// </summary>
        /// <param name="brighterBuilder">The Brighter Builder</param>
        /// <param name="timedOutboxSweeperOptionsAction">Configuration actions for the Timed outbox Sweeper <see cref="TimedOutboxSweeper"/></param>
        /// <returns>The Brighter Builder</returns>
        public static IBrighterBuilder UseOutboxSweeper(this IBrighterBuilder brighterBuilder,
            Action<TimedOutboxSweeperOptions>? timedOutboxSweeperOptionsAction = null)
        {
            var options = new TimedOutboxSweeperOptions();
            timedOutboxSweeperOptionsAction?.Invoke(options);

            brighterBuilder.Services.TryAddSingleton(options);
            brighterBuilder.Services.AddHostedService<TimedOutboxSweeper>();
            return brighterBuilder;
        }

        /// <summary>
        /// Use a timer based outbox archiver as a Hosted Service. Infers the transaction type automatically
        /// from the registered <see cref="IAmABoxTransactionProvider{T}"/>, so no generic type parameter is needed.
        /// </summary>
        /// <remarks>
        /// This overload requires that <see cref="IAmABoxTransactionProvider{T}"/> is registered with a concrete
        /// implementation type (as done by the standard <c>AddProducers</c> overload). If you are using the
        /// deferred <c>AddProducers(Func&lt;IServiceProvider, ProducersConfiguration&gt;)</c> overload, the
        /// transaction type cannot be inferred at registration time — use
        /// <see cref="UseOutboxArchiver{TTransaction}"/> and specify the type explicitly instead.
        /// </remarks>
        /// <param name="brighterBuilder">The Brighter Builder</param>
        /// <param name="archiveProvider">The archive provider to use for archiving messages</param>
        /// <param name="timedOutboxArchiverOptionsAction">Configuration actions for the Timed outbox Archiver</param>
        /// <returns>The Brighter Builder</returns>
        /// <exception cref="ConfigurationException">
        /// Thrown when no resolvable <see cref="IAmABoxTransactionProvider{T}"/> is found, or when multiple
        /// distinct transaction types are registered.
        /// </exception>
        public static IBrighterBuilder UseOutboxArchiver(this IBrighterBuilder brighterBuilder,
            IAmAnArchiveProvider archiveProvider,
            Action<TimedOutboxArchiverOptions>? timedOutboxArchiverOptionsAction = null)
        {
            var transactionTypes = ResolveTransactionTypes(brighterBuilder.Services);

            if (transactionTypes.Count == 0)
                throw new ConfigurationException(
                    $"Unable to register {nameof(UseOutboxArchiver)} - no {nameof(IAmABoxTransactionProvider<object>)} could be resolved from the service descriptors. " +
                    $"If you are using the deferred AddProducers overload, use {nameof(UseOutboxArchiver)}<TTransaction>() and specify the transaction type explicitly.");

            if (transactionTypes.Count > 1)
                throw new ConfigurationException(
                    $"Unable to register {nameof(UseOutboxArchiver)} - multiple transaction provider types were found " +
                    $"({string.Join(", ", transactionTypes.Select(t => t.Name))}). " +
                    $"Use {nameof(UseOutboxArchiver)}<TTransaction>() and specify the transaction type explicitly.");

            var genericMethod = typeof(HostedServiceCollectionExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(UseOutboxArchiver) && m.IsGenericMethod);

            genericMethod.MakeGenericMethod(transactionTypes.Single())
                .Invoke(null, new object?[] { brighterBuilder, archiveProvider, timedOutboxArchiverOptionsAction });

            return brighterBuilder;
        }

        private static HashSet<Type> ResolveTransactionTypes(IServiceCollection services)
        {
            var transactionProviderInterface = typeof(IAmABoxTransactionProvider<>);

            var fromGeneric = services
                .Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == transactionProviderInterface)
                .Select(d => d.ServiceType.GetGenericArguments()[0]);

            var types = new HashSet<Type>(fromGeneric);
            if (types.Count > 0) return types;

            var fromNonGeneric = services
                .Where(d => d.ServiceType == typeof(IAmABoxTransactionProvider) && d.ImplementationType != null)
                .SelectMany(d => d.ImplementationType!.GetInterfaces())
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == transactionProviderInterface)
                .Select(i => i.GetGenericArguments()[0]);

            return new HashSet<Type>(fromNonGeneric);
        }

        /// <summary>
        /// Use a timer based outbox archiver as a Hosted Service.
        /// </summary>
        /// <typeparam name="TTransaction">The transaction type used by the outbox</typeparam>
        /// <param name="brighterBuilder">The Brighter Builder</param>
        /// <param name="archiveProvider">The archive provider to use for archiving messages</param>
        /// <param name="timedOutboxArchiverOptionsAction">Configuration actions for the Timed outbox Archiver</param>
        /// <returns>The Brighter Builder</returns>
        public static IBrighterBuilder UseOutboxArchiver<TTransaction>(this IBrighterBuilder brighterBuilder,
            IAmAnArchiveProvider archiveProvider,
            Action<TimedOutboxArchiverOptions>? timedOutboxArchiverOptionsAction = null)
        {
            var options = new TimedOutboxArchiverOptions();
            timedOutboxArchiverOptionsAction?.Invoke(options);
            brighterBuilder.Services.AddSingleton(archiveProvider);
            brighterBuilder.Services.TryAddSingleton(options);
            brighterBuilder.Services.TryAddSingleton(provider => new OutboxArchiver<Message, TTransaction>(
                provider.GetRequiredService<IAmAnOutbox>(),
                provider.GetRequiredService<IAmAnArchiveProvider>(),
                provider.GetService<IAmARequestContextFactory>(),
                options.ArchiveBatchSize,
                provider.GetService<IAmABrighterTracer>(),
                options.Instrumentation));

            brighterBuilder.Services.AddHostedService<TimedOutboxArchiver<Message, TTransaction>>();

            return brighterBuilder;
        }
    }
}
