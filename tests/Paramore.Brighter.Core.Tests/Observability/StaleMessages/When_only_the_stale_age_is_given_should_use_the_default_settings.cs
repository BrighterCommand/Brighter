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
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleMessageMonitorOptionsDefaultsTests
{
    [Fact]
    public void When_only_the_stale_age_is_given_should_use_the_default_settings()
    {
        //Arrange
        var staleMessageAge = TimeSpan.FromMinutes(5);

        //Act
        var options = new TimedOutboxStaleMessageMonitorOptions(staleMessageAge);

        //Assert
        Assert.Equal(staleMessageAge, options.StaleMessageAge);
        Assert.Equal(100, options.CountCeiling);
        Assert.Equal(TimeSpan.FromMinutes(1), options.MeasurementInterval);
        Assert.Equal(TimeSpan.FromSeconds(10), options.MeasurementTimeout);
        Assert.True(options.MeasurementTimeout < options.MeasurementInterval);

        Assert.Equal(TimeSpan.FromDays(365), TimedOutboxStaleMessageMonitorOptions.MaxStaleMessageAge);
        Assert.Equal(1000, TimedOutboxStaleMessageMonitorOptions.MaxCountCeiling);
        Assert.Equal(TimeSpan.FromMilliseconds(4_294_967_294), TimedOutboxStaleMessageMonitorOptions.MaxMeasurementInterval);
    }
}
