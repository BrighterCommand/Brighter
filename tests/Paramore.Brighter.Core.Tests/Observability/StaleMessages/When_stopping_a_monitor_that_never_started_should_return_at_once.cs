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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StoppingAMonitorThatNeverStartedTests
{
    [Theory]
    [InlineData(false, nameof(IAmProducersConfiguration))]
    [InlineData(true, nameof(IAmProducersConfiguration))]
    [InlineData(true, nameof(IAmAnOutbox))]
    public async Task When_stopping_a_monitor_that_never_started_should_return_at_once(bool startFirst, string missingRegistration)
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        if (missingRegistration == nameof(IAmProducersConfiguration))
            services.AddSingleton<IAmAnOutbox>(new StaleMessageCountingOutbox());
        else
            services.AddSingleton<IAmProducersConfiguration>(new ProducersConfiguration());

        var builder = new ServiceCollectionBrighterBuilder(
            services,
            new ServiceCollectionSubscriberRegistry(services),
            new ServiceCollectionMessageMapperRegistryBuilder(services));
        builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options =>
            options.MeasurementInterval = TimeSpan.FromMinutes(1));

        var provider = services.BuildServiceProvider();
        var monitor = provider.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        if (startFirst)
            await Assert.ThrowsAsync<ConfigurationException>(() => monitor.StartAsync(default));

        //Act
        Task? stopping = null;
        var exception = Record.Exception(() => { stopping = monitor.StopAsync(CancellationToken.None); });

        //Assert
        Assert.Null(exception);
        Assert.True(stopping!.IsCompletedSuccessfully);
    }
}
