using System.Data.Common;
using System.Transactions;
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
            if (asbSettings == null)
                throw new ConfigurationException("AzureServiceBusSettings is not configured");
            var boxSettings = builder.Configuration.GetRequiredSection(BrighterBoxSettings.SettingsKey).Get<BrighterBoxSettings>();
            if (boxSettings == null)
                throw new ConfigurationException("BrighterBoxSettings is not configured");
            
            var environmentName = builder.Configuration[_hostingEnvironment];

            var serviceBusClientProvider = new ServiceBusChainedClientProvider(asbSettings.Endpoint,
                new ManagedIdentityCredential(), new AzureCliCredential(), new VisualStudioCredential());

            var producerRegistry = new AzureServiceBusProducerRegistryFactory(serviceBusClientProvider,
                new AzureServiceBusPublication[]
                {
                    new() {MakeChannels = OnMissingChannel.Validate, Topic = new RoutingKey("default")}
                }, boxSettings.BatchChunkSize).Create();

            var outboxSettings = new RelationalDatabaseConfiguration(boxSettings.ConnectionString, outBoxTableName: boxSettings.OutboxTableName);
            Type transactionProviderType;

            if (boxSettings.UseMsi)
            {
                if (environmentName != null && environmentName.Equals(_developmentEnvironemntName, StringComparison.InvariantCultureIgnoreCase))
                {
                    transactionProviderType = typeof(MsSqlVisualStudioConnectionProvider);
                }
                else
                {
                    transactionProviderType = typeof(MsSqlDefaultAzureConnectionProvider);
                }
            }
            else
            {
                transactionProviderType = typeof(MsSqlConnectionProvider);
            }

            builder.Services.AddBrighter()
                .UseExternalBus((configure) =>
                {
                    configure.ProducerRegistry = producerRegistry;
                    configure.Outbox = new MsSqlOutbox(outboxSettings);
                    configure.TransactionProvider = transactionProviderType;
                })
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
