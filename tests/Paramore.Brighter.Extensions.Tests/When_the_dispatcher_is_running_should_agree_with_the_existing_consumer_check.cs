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

#pragma warning disable CS0618

#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Characterises that, with the dispatcher running and nothing shut or in excess, the worst consumer remedy status
/// equals the existing consumer check status and the remedy descriptions carry the values of the existing description.
/// </summary>
[Collection(DispatcherHealthCheckCollection.NAME)]
public class ConsumerRemedyChecksAgreeWithExistingCheckTests
{
    private const string ExistingCheckName = "brighter-consumer-existing";
    private const string TakeOutOfServiceName = "brighter-consumer-take-out-of-service";
    private const string RestartName = "brighter-consumer-restart";
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("short burst", HealthStatus.Healthy)]
    [InlineData("persistent failure", HealthStatus.Degraded)]
    [InlineData("some missing", HealthStatus.Degraded)]
    [InlineData("none running", HealthStatus.Unhealthy)]
    public async Task When_the_dispatcher_is_running_should_agree_with_the_existing_consumer_check(
        string state, HealthStatus expectedStatus)
    {
        //Arrange
        await using var host = await HostInState(state);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(host.Dispatcher);
        services.AddHealthChecks()
            .AddCheck<BrighterServiceActivatorHealthCheck>(ExistingCheckName)
            .AddBrighterConsumerHealthChecks();
        await using var provider = services.BuildServiceProvider();

        //Act
        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        //Assert
        var existing = report.Entries[ExistingCheckName];
        var takeOutOfService = report.Entries[TakeOutOfServiceName];
        var restart = report.Entries[RestartName];
        var worst = (HealthStatus)Math.Min((int)takeOutOfService.Status, (int)restart.Status);
        Assert.Equal(expectedStatus, existing.Status);
        Assert.Equal(existing.Status, worst);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.TakeOutOfService), takeOutOfService.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), restart.Data[RemedyCheckConvention.RemedyKindKey]);
        if (existing.Status != HealthStatus.Healthy)
        {
            var remedyDescriptions = takeOutOfService.Description + ";" + restart.Description;
            foreach (var condition in existing.Description!.Split(';'))
                Assert.Contains(condition, remedyDescriptions);
        }
    }

    private static async Task<MultiSubscriptionReceiveHealthTestHost> HostInState(string state)
    {
        ReceiveHealthSubscription subscription = state switch
        {
            "short burst" => new ReceiveHealthSubscription("orders", 1, [2]),
            "persistent failure" => new ReceiveHealthSubscription("orders", 1, [5]),
            "some missing" => new ReceiveHealthSubscription("orders", 2),
            _ => new ReceiveHealthSubscription("orders", 1)
        };
        var host = new MultiSubscriptionReceiveHealthTestHost([subscription]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, subscription.Performers);

        switch (state)
        {
            case "short burst":
            case "persistent failure":
                await host.Transports("orders")[0].FailuresObserved.WaitAsync(s_guard);
                break;
            case "some missing":
                host.StopPerformer("orders", 1);
                await ConsumersRunning(host, 1);
                break;
            case "none running":
                host.StopPerformer("orders", 0);
                await ConsumersRunning(host, 0);
                break;
        }

        return host;
    }

    private static async Task ConsumersRunning(MultiSubscriptionReceiveHealthTestHost host, int expected)
    {
        using var timeout = new CancellationTokenSource(s_guard);
        while (host.Dispatcher.Consumers.Count() != expected)
            await Task.Delay(5, timeout.Token);
    }
}
