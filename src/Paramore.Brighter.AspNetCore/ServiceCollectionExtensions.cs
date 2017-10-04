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

            var subscriberRegistry = new AspNetSubscriberRegistry(services);
            var handlerFactory = new AspNetHandlerFactory(services);
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

            services.AddSingleton<IAmACommandProcessor>(commandProcessor);

            return new AspNetHandlerBuilder(services, subscriberRegistry);
        }
    }
}