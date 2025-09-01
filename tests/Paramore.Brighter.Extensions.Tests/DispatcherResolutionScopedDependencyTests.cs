using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Sqlite;
using Paramore.Brighter.Sqlite.EntityFrameworkCore;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class DispatcherResolutionScopedDependencyTests
{
    private IServiceProvider? _provider;
    
    [Fact]
    public void ShouldResolveIDispatcherCorrectly()
    {
        Build(new InternalBus());

        Assert.NotNull(_provider);
        //will throw if cannot be found
        _provider.GetRequiredService<IDispatcher>();
    }

    [Fact]
    public void ShouldResolveIDispatcherCorrectlyWithHost()
    {
        BuildHost(new InternalBus());

        Assert.NotNull(_provider);
        //will throw if cannot be found
        _provider.GetRequiredService<IDispatcher>();
    }

    
    private void Build(InternalBus bus)
    {
        var services = new ServiceCollection();

        AddServices(services, bus);

        _provider = services.BuildServiceProvider();
    }

    private void BuildHost(InternalBus bus)
    {
        _provider = Host.CreateDefaultBuilder([])
            .ConfigureServices((hostContext, services) =>
            {
                AddServices(services, bus);

            }).Build().Services;
    }

    private void AddServices(IServiceCollection services, InternalBus bus)
    {
        services.AddScoped<Discography>();

        services
            .AddConsumers(options =>
            {
                options.Subscriptions = new List<Subscription>
                {
                    new(new SubscriptionName("Discography"), new ChannelName("test:in-memory"),
                        new RoutingKey("add_album"), typeof(AddAlbum), messagePumpType: MessagePumpType.Reactor)
                };
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.TransformerLifetime = ServiceLifetime.Scoped;
                
                options.DefaultChannelFactory = new InMemoryChannelFactory(bus, TimeProvider.System);
            })    
            .AddProducers(configure =>
            {
                configure.ProducerRegistry = new ProducerRegistry(
                    new Dictionary<ProducerKey, IAmAMessageProducer>
                    {
                        {
                            new ProducerKey("in-memory"), new InMemoryMessageProducer(bus,
                                new FakeTimeProvider(),
                                new Publication { Topic = "test" })
                        }
                    });
                var outboxConfiguration = new RelationalDatabaseConfiguration(
                    connectionString:"Data Source=discography.db;Cache=Shared",
                    databaseName: "discography",
                    outBoxTableName: "Outbox",
                    binaryMessagePayload: false
                    );
                
                //We need this as it is a dependency of the SqliteConnectionProvider
                services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);
                
                configure.Outbox = new SqliteOutbox(outboxConfiguration, new SqliteConnectionProvider(outboxConfiguration));
                configure.TransactionProvider = typeof(SqliteEntityFrameworkTransactionProvider<Discography>);
                configure.ConnectionProvider = typeof(SqliteConnectionProvider);
                configure.MaxOutStandingMessages = 5;
                configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
            })
            .AutoFromAssemblies();
        
        services.AddHostedService<ServiceActivatorHostedService>();
    }

}
