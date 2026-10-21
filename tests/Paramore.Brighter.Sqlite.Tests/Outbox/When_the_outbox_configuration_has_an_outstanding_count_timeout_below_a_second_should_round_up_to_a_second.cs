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
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

public class OutboxSubSecondOutstandingCountTimeoutTests
{
    [Fact]
    public void When_the_outbox_configuration_has_an_outstanding_count_timeout_below_a_second_should_round_up_to_a_second()
    {
        //Arrange
        using var locked = new LockedSqliteOutbox((connectionString, tableName) =>
            new StubRelationalDatabaseConfiguration(TimeSpan.FromMilliseconds(500), connectionString, tableName));
        locked.LockFor(TimeSpan.FromSeconds(8));
        var stopwatch = Stopwatch.StartNew();

        //Act
        var exception = Record.Exception(() => locked.Outbox.GetOutstandingMessageCount(TimeSpan.Zero, new RequestContext()));
        stopwatch.Stop();

        //Assert
        Assert.IsType<SqliteException>(exception);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(4));
    }
}
