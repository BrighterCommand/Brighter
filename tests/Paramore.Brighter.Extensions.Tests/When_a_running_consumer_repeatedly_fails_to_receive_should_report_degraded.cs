#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

[Collection(LoggerCaptureCollection.NAME)]
public class ConsumerReceiveFailureHealthCheckTests
{
    [Theory]
    [InlineData(MessagePumpType.Reactor, false)]
    [InlineData(MessagePumpType.Proactor, false)]
    [InlineData(MessagePumpType.Reactor, true)]
    [InlineData(MessagePumpType.Proactor, true)]
    public async Task When_a_running_consumer_repeatedly_fails_to_receive_should_report_degraded(
        MessagePumpType messagePumpType, bool brokenCircuit)
    {
        // Arrange
        await using var host = new ReceiveHealthTestHost(messagePumpType, brokenCircuit: brokenCircuit);
        var healthCheck = host.HealthCheck;

        // Act
        host.Dispatcher.Receive();
        await host.Transport.FailuresObserved.WaitAsync(TimeSpan.FromSeconds(5));
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        var consumer = Assert.Single(host.Dispatcher.Consumers);
        Assert.Equal(ConsumerState.Open, consumer.State);
        Assert.NotNull(consumer.Job);
        Assert.False(consumer.Job!.IsCompleted);
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("receive-health", result.Description);
        Assert.Contains("5 consecutive channel failures", result.Description);
    }

    [Theory]
    [InlineData(MessagePumpType.Reactor)]
    [InlineData(MessagePumpType.Proactor)]
    public async Task When_a_failed_channel_recovers_to_an_empty_queue_should_report_healthy(
        MessagePumpType messagePumpType)
    {
        // Arrange
        await using var host = new ReceiveHealthTestHost(messagePumpType);
        var healthCheck = host.HealthCheck;
        host.Dispatcher.Receive();
        await host.Transport.FailuresObserved.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HealthStatus.Degraded, (await healthCheck.CheckHealthAsync(new HealthCheckContext())).Status);

        // Act
        host.Transport.Recover();
        await host.Transport.RecoveryObserved.WaitAsync(TimeSpan.FromSeconds(5));
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        var consumer = Assert.Single(host.Dispatcher.Consumers);
        var failures = Assert.IsAssignableFrom<IHaveAChannelFailureCount>(consumer.Performer);
        Assert.Equal(0, failures.ConsecutiveChannelFailures);
    }

    [Theory]
    [InlineData(MessagePumpType.Reactor, 1, HealthStatus.Healthy)]
    [InlineData(MessagePumpType.Proactor, 1, HealthStatus.Healthy)]
    [InlineData(MessagePumpType.Reactor, 2, HealthStatus.Healthy)]
    [InlineData(MessagePumpType.Proactor, 2, HealthStatus.Healthy)]
    [InlineData(MessagePumpType.Reactor, 3, HealthStatus.Degraded)]
    [InlineData(MessagePumpType.Proactor, 3, HealthStatus.Degraded)]
    [InlineData(MessagePumpType.Reactor, 5, HealthStatus.Degraded)]
    [InlineData(MessagePumpType.Proactor, 5, HealthStatus.Degraded)]
    public async Task When_a_failure_burst_is_below_the_configured_threshold_should_report_healthy(
        MessagePumpType messagePumpType, int failures, HealthStatus expectedDefaultStatus)
    {
        // Arrange
        await using var host = new ReceiveHealthTestHost(messagePumpType, failures);
        var defaultHealthCheck = host.HealthCheck;
        var tolerantHealthCheck = new BrighterServiceActivatorHealthCheck(host.Dispatcher, channelFailureThreshold: failures + 1);
        var strictHealthCheck = new BrighterServiceActivatorHealthCheck(host.Dispatcher, channelFailureThreshold: failures);
        host.Dispatcher.Receive();
        await host.Transport.FailuresObserved.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        var defaultResult = await defaultHealthCheck.CheckHealthAsync(new HealthCheckContext());
        var tolerantResult = await tolerantHealthCheck.CheckHealthAsync(new HealthCheckContext());
        var strictResult = await strictHealthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(expectedDefaultStatus, defaultResult.Status);
        Assert.Equal(HealthStatus.Healthy, tolerantResult.Status);
        Assert.Equal(HealthStatus.Degraded, strictResult.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task When_a_channel_failure_threshold_is_not_positive_should_reject_configuration(int threshold)
    {
        // Arrange
        await using var host = new ReceiveHealthTestHost(MessagePumpType.Reactor);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BrighterServiceActivatorHealthCheck(host.Dispatcher, threshold));

        // Assert
        Assert.Equal("channelFailureThreshold", exception.ParamName);
    }

    [Theory]
    [InlineData(MessagePumpType.Reactor)]
    [InlineData(MessagePumpType.Proactor)]
    public async Task When_all_expected_consumers_have_stopped_should_report_unhealthy(MessagePumpType messagePumpType)
    {
        // Arrange
        await using var host = new ReceiveHealthTestHost(messagePumpType);
        var healthCheck = host.HealthCheck;
        host.Dispatcher.Receive();
        await host.Transport.FailuresObserved.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HealthStatus.Degraded, (await healthCheck.CheckHealthAsync(new HealthCheckContext())).Status);

        // Act
        var shutdown = host.Dispatcher.End();
        host.Transport.Recover();
        await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Empty(host.Dispatcher.Consumers);
        Assert.Contains("receive-health has 0 of 1 expected consumers", result.Description);
    }

    [Theory]
    [InlineData(MessagePumpType.Reactor)]
    [InlineData(MessagePumpType.Proactor)]
    public async Task When_a_health_check_is_activated_without_registration_should_report_degraded(
        MessagePumpType messagePumpType)
    {
        // Arrange
        await using var host = new ReceiveHealthTestHost(messagePumpType, registerHealthCheck: false);
        var healthCheck = host.HealthCheck;

        // Act
        host.Dispatcher.Receive();
        await host.Transport.FailuresObserved.WaitAsync(TimeSpan.FromSeconds(5));
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }
}
