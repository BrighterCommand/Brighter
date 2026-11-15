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

namespace Paramore.Brighter.InMemory.Tests.Outbox;

[Property("Category", "InMemory")]
public class InMemoryOutboxPaginationTests
{
    [Test]
    [Arguments(false, false, 1)]
    [Arguments(false, true, 1)]
    [Arguments(true, false, 1)]
    [Arguments(true, true, 1)]
    [Arguments(false, false, 2)]
    [Arguments(false, true, 2)]
    [Arguments(true, false, 2)]
    [Arguments(true, true, 2)]
    [Arguments(false, false, 3)]
    [Arguments(false, true, 3)]
    [Arguments(true, false, 3)]
    [Arguments(true, true, 3)]
    [Arguments(false, false, 4)]
    [Arguments(false, true, 4)]
    [Arguments(true, false, 4)]
    [Arguments(true, true, 4)]
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
        await Assert.That(allEligible).IsEqualTo(expectedIds);
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            await Assert.That(pages[pageIndex]).IsEqualTo(expectedIds.Skip(pageIndex * pageSize).Take(pageSize));

        var pagedIds = pages.SelectMany(page => page).ToArray();
        await Assert.That(pagedIds).IsEqualTo(expectedIds);
        await Assert.That(pagedIds.Distinct().Count()).IsEqualTo(3);
        await Assert.That(pages[3]).IsEmpty();
        await Assert.That(firstPageAgain).IsEqualTo(pages[0]);
        await Assert.That(zeroSizePage).IsEmpty();
        await Assert.That(farPage).IsEmpty();
        await Assert.That(maximumPage).IsEmpty();
    }
}
