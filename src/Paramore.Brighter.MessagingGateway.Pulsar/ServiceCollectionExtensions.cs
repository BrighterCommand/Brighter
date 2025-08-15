using System;
using DotPulsar;
using DotPulsar.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.MessagingGateway.Pulsar
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBrighterPulsar(
            this IServiceCollection services,
            PulsarMessagingGatewayConfiguration config,
            Publication publication)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (publication == null)
                throw new ArgumentNullException(nameof(publication));

            config.Validate();

            services.AddSingleton(config);
            services.AddSingleton<IPulsarClient>(_ =>
                PulsarClient.Builder().ServiceUrl(new Uri(config.ServiceUrl)).Build());

            services.AddSingleton<IAmAMessageProducer>(sp =>
                new PulsarMessageProducer(config, publication, sp.GetRequiredService<IPulsarClient>()));

            services.AddSingleton<IAmAMessageConsumerAsync>(sp =>
                new PulsarMessageConsumer(config, sp.GetRequiredService<IPulsarClient>()));

            return services;
        }
    }
}
