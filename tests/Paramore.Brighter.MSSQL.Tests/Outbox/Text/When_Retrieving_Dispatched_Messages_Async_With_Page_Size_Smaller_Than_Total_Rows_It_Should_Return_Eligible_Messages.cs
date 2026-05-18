#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox.Text;

/// <summary>
/// Async regression test for the ROW_NUMBER pagination bug in MsSqlOutbox.DispatchedMessagesAsync.
///
/// When the outbox contains more total rows than the requested pageSize, the original
/// PagedDispatchedCommand computes ROW_NUMBER() across ALL rows before filtering by
/// [Dispatched]. This causes eligible dispatched rows to be assigned row numbers higher
/// than the page window and silently return zero results.
///
/// The fix is to move the [Dispatched] filter inside the subquery so ROW_NUMBER() is
/// computed only over the eligible set.
/// </summary>
public class WhenRetrievingDispatchedMessagesAsyncWithPageSizeSmallerThanTotalRowsItShouldReturnEligibleMessages
    : IAsyncLifetime
{
    private readonly MSSQLTextOutboxProvider _outboxProvider;
    private readonly DefaultMessageFactory _messageFactory;
    private readonly List<Message> _createdMessages = [];

    public WhenRetrievingDispatchedMessagesAsyncWithPageSizeSmallerThanTotalRowsItShouldReturnEligibleMessages()
    {
        _outboxProvider = new MSSQLTextOutboxProvider();
        _messageFactory = new DefaultMessageFactory();
    }

    public async Task InitializeAsync()
    {
        await _outboxProvider.CreateStoreAsync();
    }

    public async Task DisposeAsync()
    {
        await _outboxProvider.DeleteStoreAsync(_createdMessages);
    }

    [Fact]
    public async Task When_Page_Size_Is_Smaller_Than_Total_Row_Count_Dispatched_Messages_Should_Still_Be_Returned_Async()
    {
        // Arrange
        const int pageSize = 5;
        const int undispatchedCount = 10; // more than pageSize — fills ROW_NUMBER slots 1-10 in the buggy query

        var context = new RequestContext();
        var outbox = _outboxProvider.CreateOutboxAsync();

        // Add undispatched messages (most recent — they occupy the lowest ROW_NUMBER values
        // in the buggy query that orders by [Timestamp] DESC across ALL rows)
        for (var i = 0; i < undispatchedCount; i++)
        {
            var msg = _messageFactory.Create(DateTimeOffset.UtcNow.AddMinutes(-i));
            _createdMessages.Add(msg);
            await outbox.AddAsync([msg], context);
        }

        // Add two dispatched messages (older — dispatched more than 2 hours ago).
        // With the bug these receive ROW_NUMBER 11 and 12, which lie outside BETWEEN 1 AND 5.
        var dispatchedOld1 = _messageFactory.Create(DateTimeOffset.UtcNow.AddHours(-3));
        var dispatchedOld2 = _messageFactory.Create(DateTimeOffset.UtcNow.AddHours(-4));
        _createdMessages.Add(dispatchedOld1);
        _createdMessages.Add(dispatchedOld2);
        await outbox.AddAsync([dispatchedOld1, dispatchedOld2], context);
        await outbox.MarkDispatchedAsync(dispatchedOld1.Id, context, DateTime.UtcNow.AddHours(-3));
        await outbox.MarkDispatchedAsync(dispatchedOld2.Id, context, DateTime.UtcNow.AddHours(-4));

        // Act — request page 1 with pageSize smaller than the total row count
        var results = (await outbox.DispatchedMessagesAsync(
            TimeSpan.FromHours(2),
            context,
            pageSize: pageSize,
            pageNumber: 1
        )).ToArray();

        // Assert — both eligible dispatched messages must be returned regardless of how many
        // undispatched rows exist in the table
        Assert.Contains(dispatchedOld1.Id, results.Select(m => m.Id));
        Assert.Contains(dispatchedOld2.Id, results.Select(m => m.Id));
    }
}
