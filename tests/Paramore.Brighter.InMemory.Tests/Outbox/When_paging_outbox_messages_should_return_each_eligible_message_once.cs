#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia

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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox;

[Trait("Category", "InMemory")]
public class InMemoryOutboxPaginationTests
{
    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, false, 2)]
    [InlineData(true, true, 2)]
    [InlineData(false, false, 3)]
    [InlineData(false, true, 3)]
    [InlineData(true, false, 3)]
    [InlineData(true, true, 3)]
    [InlineData(false, false, 4)]
    [InlineData(false, true, 4)]
    [InlineData(true, false, 4)]
    [InlineData(true, true, 4)]
    public async Task When_paging_outbox_messages_should_return_each_eligible_message_once(
        bool dispatched, bool useAsync, int pageSize)
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var outbox = new InMemoryOutbox(timeProvider) { EntryLimit = -1 };
        var context = new RequestContext();
        var timestamp = timeProvider.GetUtcNow();
        string[] eligibleIds = ["later", "early-b", "early-a"];
        string[] expectedIds = ["early-a", "early-b", "later"];
        RoutingKey[] trippedTopics = [new("blocked-topic")];

        foreach (var id in eligibleIds)
        {
            outbox.Add(new MessageTestDataBuilder().With(message => message.Header =
                new MessageHeader(id, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT)
                {
                    TimeStamp = id == "later" ? timestamp.AddSeconds(1) : timestamp
                }), context);
            if (dispatched)
                outbox.MarkDispatched(id, context);
        }

        outbox.Add(new MessageTestDataBuilder().With(message => message.Header =
            new MessageHeader("opposite-state", new RoutingKey("test_topic"), MessageType.MT_DOCUMENT)
            {
                TimeStamp = timestamp.AddSeconds(-1)
            }), context);
        if (!dispatched)
        {
            outbox.MarkDispatched("opposite-state", context);
            outbox.Add(new MessageTestDataBuilder().With(message => message.Header =
                new MessageHeader("blocked", trippedTopics[0], MessageType.MT_DOCUMENT)
                {
                    TimeStamp = timestamp.AddSeconds(-1)
                }), context);
        }

        timeProvider.Advance(TimeSpan.FromSeconds(10));
        outbox.Add(new MessageTestDataBuilder().With(message => message.Header =
            new MessageHeader("too-recent", new RoutingKey("test_topic"), MessageType.MT_DOCUMENT)
            {
                TimeStamp = timestamp.AddSeconds(-1)
            }), context);
        if (dispatched)
            outbox.MarkDispatched("too-recent", context);

        var age = TimeSpan.FromSeconds(5);

        async Task<string[]> ReadPage(int number, int size)
        {
            IEnumerable<Message> messages;
            if (dispatched)
            {
                messages = useAsync
                    ? await outbox.DispatchedMessagesAsync(age, context, size, number)
                    : outbox.DispatchedMessages(age, context, size, number);
            }
            else
            {
                messages = useAsync
                    ? await outbox.OutstandingMessagesAsync(age, context, size, number, trippedTopics)
                    : outbox.OutstandingMessages(age, context, size, number, trippedTopics);
            }

            return messages.Select(message => message.Id.Value).ToArray();
        }

        //Act
        var allEligible = await ReadPage(1, 100);
        var pages = new List<string[]>();
        for (var pageNumber = 1; pageNumber <= 4; pageNumber++)
            pages.Add(await ReadPage(pageNumber, pageSize));

        var firstPageAgain = await ReadPage(1, pageSize);
        var zeroSizePage = await ReadPage(1, 0);
        var farPage = await ReadPage(1_073_741_825, 4);
        var maximumPage = await ReadPage(int.MaxValue, int.MaxValue);

        //Assert
        Assert.Equal(expectedIds, allEligible);
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            Assert.Equal(expectedIds.Skip(pageIndex * pageSize).Take(pageSize), pages[pageIndex]);

        var pagedIds = pages.SelectMany(page => page).ToArray();
        Assert.Equal(expectedIds, pagedIds);
        Assert.Equal(3, pagedIds.Distinct().Count());
        Assert.Empty(pages[3]);
        Assert.Equal(pages[0], firstPageAgain);
        Assert.Empty(zeroSizePage);
        Assert.Empty(farPage);
        Assert.Empty(maximumPage);
    }
}
