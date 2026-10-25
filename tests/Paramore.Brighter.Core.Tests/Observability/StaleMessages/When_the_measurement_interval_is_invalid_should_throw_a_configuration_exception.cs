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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleMessageMonitorInvalidMeasurementIntervalTests
{
    public static TheoryData<TimeSpan> InvalidMeasurementIntervals => new()
    {
        TimeSpan.Zero,
        TimeSpan.FromDays(50)
    };

    [Theory]
    [MemberData(nameof(InvalidMeasurementIntervals))]
    public void When_the_measurement_interval_is_invalid_should_throw_a_configuration_exception(TimeSpan measurementInterval)
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = new ServiceCollectionBrighterBuilder(
            services,
            new ServiceCollectionSubscriberRegistry(services),
            new ServiceCollectionMessageMapperRegistryBuilder(services));
        var registrationsBefore = services.ToList();

        //Act
        var exception = Assert.Throws<ConfigurationException>(() =>
            builder.UseOutboxStaleMessageMonitor(
                TimeSpan.FromMinutes(5),
                options => options.MeasurementInterval = measurementInterval));

        //Assert
        Assert.Contains(nameof(TimedOutboxStaleMessageMonitorOptions.MeasurementInterval), exception.Message);
        Assert.Contains(measurementInterval.ToString(), exception.Message);
        Assert.Equal(registrationsBefore, services);
    }
}
