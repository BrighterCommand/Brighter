#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel <guilhermeaappel@gmail.com>

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

using System;
using System.Threading;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

public class OutOfRangeOutstandingCountTimeoutTests
{
    public static TheoryData<TimeSpan> OutOfRangeTimeouts => new()
    {
        TimeSpan.Zero,
        TimeSpan.FromSeconds(-1),
        Timeout.InfiniteTimeSpan,
        TimeSpan.FromSeconds(int.MaxValue / 1000 + 0.5),
        TimeSpan.FromSeconds(int.MaxValue / 1000 + 1),
        TimeSpan.MaxValue
    };

    [Theory]
    [MemberData(nameof(OutOfRangeTimeouts))]
    public void When_the_outstanding_count_timeout_is_out_of_range_should_throw(TimeSpan outstandingCountTimeout)
    {
        //Act
        var exception = Record.Exception(() => new RelationalDatabaseConfiguration(
            "Data Source=:memory:", outstandingCountTimeout: outstandingCountTimeout));

        //Assert
        var outOfRange = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("outstandingCountTimeout", outOfRange.ParamName);
    }
}
