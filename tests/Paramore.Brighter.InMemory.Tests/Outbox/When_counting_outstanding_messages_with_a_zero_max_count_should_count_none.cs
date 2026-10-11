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
public class InMemoryOutboxOutstandingCountZeroLimitTests
{
    [Fact]
    public void When_counting_outstanding_messages_with_a_zero_max_count_should_count_none()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };
        var context = new RequestContext();
        const int maxCount = 0;

        //Three outstanding entries, all older than the interval when the count is made
        outbox.Add(new MessageTestDataBuilder(), context);
        outbox.Add(new MessageTestDataBuilder(), context);
        outbox.Add(new MessageTestDataBuilder(), context);

        timeProvider.Advance(TimeSpan.FromSeconds(10));

        //Act
        var count = outbox.GetOutstandingMessageCount(TimeSpan.FromSeconds(5), context, maxCount);

        //Assert
        Assert.Equal(0, count);
    }
}
