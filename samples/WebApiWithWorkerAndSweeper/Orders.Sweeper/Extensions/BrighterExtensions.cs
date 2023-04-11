using Azure.Identity;
using Orders.Sweeper.Settings;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MsSql.Azure;
using Paramore.Brighter.Outbox.MsSql;
using ServiceBusChainedClientProvider = Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider.ServiceBusChainedClientProvider;

namespace Orders.Sweeper.Extensions;

public static class BrighterExtensions
    {
        private const string _hostingEnvironment = "ASPNETCORE_ENVIRONMENT";
        private const string _developmentEnvironemntName = "Development";
        
        public static WebApplicationBuilder AddBrighter(this WebApplicationBuilder builder)
        {
            var asbSettings = builder.Configuration.GetRequiredSection(AzureServiceBusSettings.SettingsKey).Get<AzureServiceBusSettings>();
            var boxSettings = builder.Configuration.GetRequiredSection(BrighterBoxSettings.SettingsKey).Get<BrighterBoxSettings>();
            
            var environmentName = builder.Configuration[_hostingEnvironment];

            var serviceBusClientProvider = new ServiceBusChainedClientProvider(asbSettings.Endpoint,
                new ManagedIdentityCredential(), new AzureCliCredential(), new VisualStudioCredential());

            var producerRegistry = new AzureServiceBusProducerRegistryFactory(serviceBusClientProvider,
                new AzureServiceBusPublication[]
                {
                    new() {MakeChannels = OnMissingChannel.Validate, Topic = new RoutingKey("default")}
                }, boxSettings.BatchChunkSize).Create();

            var outboxSettings = new MsSqlConfiguration(boxSettings.ConnectionString, boxSettings.OutboxTableName);
            Type outboxType;

            if (boxSettings.UseMsi)
            {
                if (environmentName != null && environmentName.Equals(_developmentEnvironemntName, StringComparison.InvariantCultureIgnoreCase))
                {
                    outboxType = typeof(MsSqlVisualStudioConnectionProvider);
                }
                else
                {
                    outboxType = typeof(MsSqlDefaultAzureConnectionProvider);
                }
            }
            else
            {
                outboxType = typeof(MsSqlSqlAuthConnectionProvider);
            }

            builder.Services.AddBrighter()
                .UseExternalBus(producerRegistry)
                .UseMsSqlOutbox(outboxSettings, outboxType)
                .UseOutboxSweeper(options =>
                {
                    options.TimerInterval = boxSettings.OutboxSweeperInterval;
                    options.MinimumMessageAge = boxSettings.MinimumMessageAge;
                    options.BatchSize = boxSettings.BatchSize;
                    options.UseBulk = boxSettings.UseBulk;
                });

            return builder;
        }
    }
