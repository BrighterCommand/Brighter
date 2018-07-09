using System;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.AspNetCore
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterHandlerBuilder AddBrighter(this IServiceCollection services, Action<BrighterOptions> configure = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var options = new BrighterOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);

            var subscriberRegistry = new AspNetSubscriberRegistry(services, options.HandlerLifetime);
            services.AddSingleton<AspNetSubscriberRegistry>(subscriberRegistry);

            services.AddSingleton<IAmACommandProcessor>(BuildCommandProcessor);

            return new AspNetHandlerBuilder(services, subscriberRegistry);
        }

        private static CommandProcessor BuildCommandProcessor(IServiceProvider provider)
        {
            var options = provider.GetService<BrighterOptions>();
            var subscriberRegistry = provider.GetService<AspNetSubscriberRegistry>();

            var handlerFactory = new AspNetHandlerFactory(provider);
            var handlerConfiguration = new HandlerConfiguration(subscriberRegistry, handlerFactory, handlerFactory);

            var policyBuilder = CommandProcessorBuilder.With()
                .Handlers(handlerConfiguration);

            var messagingBuilder = options.PolicyRegistry == null
                ? policyBuilder.DefaultPolicy()
                : policyBuilder.Policies(options.PolicyRegistry);

            var builder = options.MessagingConfiguration == null
                ? messagingBuilder.NoTaskQueues()
                : messagingBuilder.TaskQueues(options.MessagingConfiguration);

            var commandProcessor = builder
                .RequestContextFactory(options.RequestContextFactory)
                .Build();

            return commandProcessor;
        }
    }
}