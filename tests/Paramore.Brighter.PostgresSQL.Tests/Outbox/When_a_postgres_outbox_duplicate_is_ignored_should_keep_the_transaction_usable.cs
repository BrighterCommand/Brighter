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

using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

[Trait("Category", "PostgresSql")]
public class PostgreSqlOutboxDuplicateTransactionTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, true, true)]
    public async Task When_a_postgres_outbox_duplicate_is_ignored_should_keep_the_transaction_usable(
        bool useAsync, bool binaryPayload, bool hasCausation, bool useBatch)
    {
        //Arrange
        using var store = new PostgreSqlOutboxTestStore(binaryPayload, hasCausation);
        var outbox = new PostgreSqlOutbox(store.Configuration);
        var context = new RequestContext();
        var original = PostgreSqlOutboxTestStore.MessageWith(Id.Random(), "original body");
        var duplicate = PostgreSqlOutboxTestStore.MessageWith(original.Id, "replacement body");
        var fresh = PostgreSqlOutboxTestStore.MessageWith(Id.Random(), "new body");
        outbox.Add(original, context);
        var transaction = new PostgreSqlTransactionProvider(store.Configuration);
        try
        {
            if (useAsync)
                await transaction.GetTransactionAsync();
            else
                transaction.GetTransaction();

            //Act
            if (useAsync)
            {
                if (useBatch)
                    await outbox.AddAsync(new[] { duplicate }, context, transactionProvider: transaction);
                else
                    await outbox.AddAsync(duplicate, context, transactionProvider: transaction);
                await outbox.AddAsync(fresh, context, transactionProvider: transaction);
                await transaction.CommitAsync(CancellationToken.None);
            }
            else
            {
                if (useBatch)
                    outbox.Add(new[] { duplicate }, context, transactionProvider: transaction);
                else
                    outbox.Add(duplicate, context, transactionProvider: transaction);
                outbox.Add(fresh, context, transactionProvider: transaction);
                transaction.Commit();
            }
        }
        finally
        {
            transaction.Dispose();
        }

        //Assert
        Assert.Equal(2L, store.Count());
        Assert.Equal(original.Body.Value, outbox.Get(original.Id, context).Body.Value);
        Assert.Equal(fresh.Body.Value, outbox.Get(fresh.Id, context).Body.Value);
    }
}
