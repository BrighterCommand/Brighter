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

using System;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlBoxMigrationRunnerLockTimeoutValidationTests
{
    private static readonly RelationalDatabaseConfiguration AnyConfig =
        new(Configuration.DefaultConnectingString, outBoxTableName: "any_table_name");

    [Fact]
    public void When_lock_timeout_exceeds_int_max_milliseconds_runner_construction_should_throw()
    {
        //Arrange — sp_getapplock takes @LockTimeout as INT (milliseconds), so any TimeSpan
        //whose TotalMilliseconds exceeds int.MaxValue (~24.85 days) silently overflows when
        //cast and may produce -1, which sp_getapplock interprets as "wait indefinitely".
        //Construction must fail fast.
        var overflowingTimeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1);

        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MsSqlBoxMigrationRunner(AnyConfig, overflowingTimeout));
    }

    [Fact]
    public void When_lock_timeout_is_negative_runner_construction_should_throw()
    {
        //Arrange — a negative timeout has no meaningful interpretation for an exclusive
        //application lock and would also overflow into sp_getapplock's reserved values.
        var negativeTimeout = TimeSpan.FromMilliseconds(-1);

        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MsSqlBoxMigrationRunner(AnyConfig, negativeTimeout));
    }

    [Fact]
    public void When_lock_timeout_is_at_int_max_milliseconds_construction_should_succeed()
    {
        //Arrange — the inclusive upper bound (~24.85 days) is the largest value that fits
        //sp_getapplock's INT @LockTimeout argument without overflow; it must be accepted.
        var boundaryTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

        //Act
        var ex = Record.Exception(() => new MsSqlBoxMigrationRunner(AnyConfig, boundaryTimeout));

        //Assert
        Assert.Null(ex);
    }
}
