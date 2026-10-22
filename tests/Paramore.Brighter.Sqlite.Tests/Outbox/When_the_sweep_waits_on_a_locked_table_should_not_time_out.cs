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
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

public class OutstandingSweepNoTimeoutTests
{
    [Fact]
    public void When_the_sweep_waits_on_a_locked_table_should_not_time_out()
    {
        //Arrange
        using var locked = new LockedSqliteOutbox();
        locked.LockFor(TimeSpan.FromSeconds(18));
        var stopwatch = Stopwatch.StartNew();

        //Act
        IEnumerable<Message> messages = null;
        var exception = Record.Exception(() => messages = locked.Outbox.OutstandingMessages(TimeSpan.Zero, new RequestContext()));
        stopwatch.Stop();

        //Assert
        Assert.Null(exception);
        Assert.Single(messages);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromSeconds(16), $"The sweep returned after {stopwatch.Elapsed}, before the lock was released");
    }
}
