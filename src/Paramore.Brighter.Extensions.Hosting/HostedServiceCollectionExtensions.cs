using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.Hosting
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
            Action<TimedOutboxSweeperOptions> timedOutboxSweeperOptionsAction = null)
        {
            var options = new TimedOutboxSweeperOptions();
            timedOutboxSweeperOptionsAction?.Invoke(options);
            
            brighterBuilder.Services.TryAddSingleton<TimedOutboxSweeperOptions>(options);
            brighterBuilder.Services.AddHostedService<TimedOutboxSweeper>();
            return brighterBuilder;
        }

        public static IBrighterBuilder UseOutboxArchiver<TTransaction>(this IBrighterBuilder brighterBuilder,
            IAmAnArchiveProvider archiveProvider,
            Action<TimedOutboxArchiverOptions> timedOutboxArchiverOptionsAction = null)
        {
            var options = new TimedOutboxArchiverOptions();
            timedOutboxArchiverOptionsAction?.Invoke(options);
            brighterBuilder.Services.TryAddSingleton<TimedOutboxArchiverOptions>(options);
            brighterBuilder.Services.AddSingleton<IAmAnArchiveProvider>(archiveProvider);
            
            brighterBuilder.Services.AddHostedService<TimedOutboxArchiver<Message, TTransaction>>();

            return brighterBuilder;
        }
    }
}
