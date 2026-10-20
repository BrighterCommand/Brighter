using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// Builds the service collection a sweeper test needs: <c>AddBrighter</c>, producers, the test's
/// <see cref="IDistributedLock"/> double, a <see cref="TimeProvider"/> and, optionally, <c>UseOutboxSweeper</c>.
/// </summary>
public static class SweeperContainer
{
    public static IServiceCollection Create(
        IDistributedLock distributedLock,
        TimeProvider timeProvider,
        bool useOutboxSweeper = false,
        IMeterFactory? meterFactory = null,
        Action<TimedOutboxSweeperOptions>? configureSweeper = null)
    {
        var routingKey = new RoutingKey("MyCommand");
        var producer = new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey });
        var producers = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

        var services = new ServiceCollection();
        services.AddSingleton(distributedLock);
        services.AddSingleton(timeProvider);
        if (meterFactory is not null) services.AddSingleton(meterFactory);

        var builder = services.AddBrighter().AddProducers(configuration => configuration.ProducerRegistry = producers);
        if (useOutboxSweeper) builder.UseOutboxSweeper(configureSweeper);

        return services;
    }
}
