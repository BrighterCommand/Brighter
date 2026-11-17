#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

public class MsSqlMessageIdBulkRetryTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task When_mssql_bulk_add_contains_duplicate_ids_should_preserve_each_distinct_message(
        bool binary, bool useAsync, bool transactional)
    {
        //Arrange
        await using var store = new MsSqlMessageIdOutboxStore(binary);
        await store.InitializeAsync();
        var outbox = new MsSqlOutbox(store.OutboxConfig);
        var context = new RequestContext();
        var existing = MsSqlMessageIdOutboxStore.Message(Id.Random(), "existing payload");
        var retry = MsSqlMessageIdOutboxStore.Message(existing.Id, "replacement must not win");
        var first = MsSqlMessageIdOutboxStore.Message(Id.Random(), "first new payload");
        var second = MsSqlMessageIdOutboxStore.Message(Id.Random(), "second new payload");
        var following = MsSqlMessageIdOutboxStore.Message(Id.Random(), "following write");
        await outbox.AddAsync(existing, context);
        Message[] batch = [first, retry, second, first, retry];
        var transactionProvider = transactional ? new MsSqlTransactionProvider(store.Config) : null;
        try
        {
            if (transactionProvider is not null)
                await transactionProvider.GetTransactionAsync();

            //Act
            if (useAsync)
            {
                await outbox.AddAsync(batch, context, transactionProvider: transactionProvider);
                await outbox.AddAsync(following, context, transactionProvider: transactionProvider);
            }
            else
            {
                outbox.Add(batch, context, transactionProvider: transactionProvider);
                outbox.Add(following, context, transactionProvider: transactionProvider);
            }
            transactionProvider?.Commit();
        }
        finally
        {
            transactionProvider?.Dispose();
        }

        //Assert
        Assert.Equal(4, await store.RowCountAsync());
        var stored = (await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).ToArray();
        Assert.Equal(4, stored.Length);
        foreach (var expected in new[] { existing, first, second, following })
        {
            var actual = Assert.Single(stored, message => message.Id == expected.Id);
            Assert.Equal(expected.Body.ToByteArray(), actual.Body.ToByteArray());
        }
    }
}
