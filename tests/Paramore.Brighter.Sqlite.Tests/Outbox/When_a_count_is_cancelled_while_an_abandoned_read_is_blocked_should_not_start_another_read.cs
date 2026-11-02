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
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

public class AbandonedOutstandingCountReadTests
{
    [Fact]
    public async Task When_a_count_is_cancelled_while_an_abandoned_read_is_blocked_should_not_start_another_read()
    {
        //Arrange
        using var locked = new LockedSqliteOutbox();
        locked.LockFor(TimeSpan.FromSeconds(10));
        var readsBeforeTheCounts = locked.AsyncConnections;
        using var firstCall = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var first = await Record.ExceptionAsync(() => locked.Outbox.GetOutstandingMessageCountAsync(
            TimeSpan.Zero, new RequestContext(), cancellationToken: firstCall.Token));

        //Act
        using var secondCall = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var second = await Record.ExceptionAsync(() => locked.Outbox.GetOutstandingMessageCountAsync(
            TimeSpan.Zero, new RequestContext(), cancellationToken: secondCall.Token));

        //Assert
        Assert.IsAssignableFrom<OperationCanceledException>(first);
        Assert.IsAssignableFrom<OperationCanceledException>(second);
        Assert.Equal(1, locked.AsyncConnections - readsBeforeTheCounts);
    }
}
