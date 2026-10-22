#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Verifies the opt-in call validates the channel failure threshold before registering anything.
/// </summary>
[Collection(DispatcherHealthCheckCollection.NAME)]
public class ConsumerHealthChecksThresholdValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void When_consumer_checks_are_added_with_a_non_positive_threshold_should_throw_before_registering(int threshold)
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();
        services.AddOptions();

        //Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddBrighterConsumerHealthChecks(threshold));

        //Assert
        Assert.Equal("channelFailureThreshold", exception.ParamName);
        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Empty(registrations);
    }

    [Fact]
    public async Task When_consumer_checks_are_added_with_no_threshold_should_degrade_at_three_failures()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2, [0, 3])]);
        host.Dispatcher.Receive();
        while (host.Transports("orders").Count < 2)
            await Task.Delay(5);
        await Task.WhenAll(host.Transports("orders").Select(transport => transport.FailuresObserved)).WaitAsync(TimeSpan.FromSeconds(10));
        var services = new ServiceCollection();
        services.AddSingleton<IDispatcher>(host.Dispatcher);

        //Act
        services.AddHealthChecks().AddBrighterConsumerHealthChecks();
        using var provider = services.BuildServiceProvider();
        var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Single(candidate => candidate.Name == "brighter-consumer-take-out-of-service");
        var result = await registration.Factory(provider).CheckHealthAsync(new HealthCheckContext { Registration = registration });

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }
}
