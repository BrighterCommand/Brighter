#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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

#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleMessageMonitorMissingProducersTests
{
    [Theory]
    [InlineData(nameof(IAmProducersConfiguration), nameof(IAmAnOutbox))]
    [InlineData(nameof(IAmAnOutbox), nameof(IAmProducersConfiguration))]
    public async Task When_the_monitor_starts_without_producers_should_throw_a_configuration_exception(string missingRegistration, string registeredService)
    {
        //Arrange
        var interval = TimeSpan.FromMinutes(1);
        var timeProvider = new FakeTimeProvider();
        var outbox = new StaleMessageCountingOutbox();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        if (missingRegistration == nameof(IAmProducersConfiguration))
            services.AddSingleton<IAmAnOutbox>(outbox);
        else
            services.AddSingleton<IAmProducersConfiguration>(new ProducersConfiguration());

        var builder = new ServiceCollectionBrighterBuilder(
            services,
            new ServiceCollectionSubscriberRegistry(services),
            new ServiceCollectionMessageMapperRegistryBuilder(services));
        builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options => options.MeasurementInterval = interval);

        var provider = services.BuildServiceProvider();
        var monitor = provider.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = provider.GetRequiredService<OutboxStaleMessageReadings>();

        //Act
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => monitor.StartAsync(default));
        timeProvider.Advance(interval);

        //Assert
        Assert.Contains(missingRegistration, exception.Message);
        Assert.DoesNotContain(registeredService, exception.Message);
        Assert.Contains("AddProducers", exception.Message);
        Assert.False(readings.IsStarted);
        Assert.Empty(outbox.CountCalls);
    }
}
