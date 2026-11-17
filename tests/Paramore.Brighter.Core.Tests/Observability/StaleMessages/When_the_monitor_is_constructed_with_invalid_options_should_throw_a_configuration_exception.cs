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
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleMessageMonitorConstructionValidationTests
{
    [Fact]
    public void When_the_monitor_is_constructed_with_invalid_options_should_throw_a_configuration_exception()
    {
        //Arrange
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var options = new TimedOutboxStaleMessageMonitorOptions(TimeSpan.FromMinutes(5));
        options.MeasurementTimeout = options.MeasurementInterval;

        //Act
        var exception = Assert.Throws<ConfigurationException>(() =>
            new TimedOutboxStaleMessageMonitor(scopeFactory, new OutboxStaleMessageReadings(), options));

        //Assert
        Assert.Contains(nameof(TimedOutboxStaleMessageMonitorOptions.MeasurementTimeout), exception.Message);
        Assert.Contains(options.MeasurementTimeout.ToString(), exception.Message);
    }
}
