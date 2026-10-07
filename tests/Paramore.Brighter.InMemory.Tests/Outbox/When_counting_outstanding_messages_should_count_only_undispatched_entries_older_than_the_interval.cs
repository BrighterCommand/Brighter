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

using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox;

[Trait("Category", "InMemory")]
public class InMemoryOutboxOutstandingCountEligibilityTests
{
    [Fact]
    public void When_counting_outstanding_messages_should_count_only_undispatched_entries_older_than_the_interval()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };
        var context = new RequestContext();
        var interval = TimeSpan.FromSeconds(5);

        //Written at the start: two stay outstanding, two are dispatched
        outbox.Add(new MessageTestDataBuilder().WithId("old-outstanding-1"), context);
        outbox.Add(new MessageTestDataBuilder().WithId("old-outstanding-2"), context);
        outbox.Add(new MessageTestDataBuilder().WithId("old-dispatched-1"), context);
        outbox.Add(new MessageTestDataBuilder().WithId("old-dispatched-2"), context);
        outbox.MarkDispatched("old-dispatched-1", context);
        outbox.MarkDispatched("old-dispatched-2", context);

        //Written exactly one interval before the count is made, so on the boundary of being counted
        timeProvider.Advance(TimeSpan.FromSeconds(3));
        outbox.Add(new MessageTestDataBuilder().WithId("boundary-outstanding"), context);

        //Written less than one interval before the count is made, so too young to be counted. There are four of
        //them, so counting the young entries instead of the old ones gives 4, or 5 with the boundary entry, never 3
        timeProvider.Advance(TimeSpan.FromSeconds(3));
        outbox.Add(new MessageTestDataBuilder().WithId("young-outstanding-1"), context);
        outbox.Add(new MessageTestDataBuilder().WithId("young-outstanding-2"), context);
        outbox.Add(new MessageTestDataBuilder().WithId("young-outstanding-3"), context);
        outbox.Add(new MessageTestDataBuilder().WithId("young-outstanding-4"), context);

        timeProvider.Advance(TimeSpan.FromSeconds(2));

        //Act
        var count = outbox.GetOutstandingMessageCount(interval, context, maxCount: 100);

        //Assert
        Assert.Equal(3, count);
    }
}
