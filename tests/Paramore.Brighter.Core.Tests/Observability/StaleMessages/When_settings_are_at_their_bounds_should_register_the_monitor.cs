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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleMessageMonitorBoundarySettingsTests
{
    public static TheoryData<TimeSpan, int, TimeSpan, TimeSpan> BoundarySettings => new()
    {
        { TimeSpan.FromMilliseconds(1), 1, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(59) },
        { TimeSpan.FromDays(365), 1000, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10) },
        { TimeSpan.FromMinutes(5), 100, TimeSpan.FromMilliseconds(4_294_967_294), TimeSpan.FromSeconds(10) }
    };

    [Theory]
    [MemberData(nameof(BoundarySettings))]
    public void When_settings_are_at_their_bounds_should_register_the_monitor(
        TimeSpan staleMessageAge, int countCeiling, TimeSpan measurementInterval, TimeSpan measurementTimeout)
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = new ServiceCollectionBrighterBuilder(
            services,
            new ServiceCollectionSubscriberRegistry(services),
            new ServiceCollectionMessageMapperRegistryBuilder(services));

        //Act
        builder.UseOutboxStaleMessageMonitor(
            staleMessageAge,
            options =>
            {
                options.CountCeiling = countCeiling;
                options.MeasurementInterval = measurementInterval;
                options.MeasurementTimeout = measurementTimeout;
            });

        //Assert
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TimedOutboxStaleMessageMonitorOptions));
    }
}
