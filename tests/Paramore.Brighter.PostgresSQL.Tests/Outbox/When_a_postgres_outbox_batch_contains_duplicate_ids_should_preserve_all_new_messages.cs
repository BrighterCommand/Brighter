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

using System.Threading.Tasks;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

[Trait("Category", "PostgresSql")]
public class PostgreSqlBulkDuplicatePreservationTests
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
    public async Task When_a_postgres_outbox_batch_contains_duplicate_ids_should_preserve_all_new_messages(
        bool useAsync, bool binaryPayload, bool hasCausation, bool alreadyStored)
    {
        //Arrange
        using var store = new PostgreSqlOutboxTestStore(binaryPayload, hasCausation);
        var outbox = new PostgreSqlOutbox(store.Configuration);
        var originalContext = new RequestContext();
        originalContext.Bag[RequestContextBagNames.CausationId] = "original-causation";
        var batchContext = new RequestContext();
        batchContext.Bag[RequestContextBagNames.CausationId] = "batch-causation";
        var original = PostgreSqlOutboxTestStore.MessageWith(Id.Random(), "original body");
        var duplicate = PostgreSqlOutboxTestStore.MessageWith(original.Id, "replacement body");
        var before = PostgreSqlOutboxTestStore.MessageWith(Id.Random(), "before duplicate");
        var after = PostgreSqlOutboxTestStore.MessageWith(Id.Random(), "after duplicate");
        if (alreadyStored)
        {
            if (useAsync)
                await outbox.AddAsync(original, originalContext);
            else
                outbox.Add(original, originalContext);
        }

        Message[] batch = alreadyStored
            ? [before, duplicate, after]
            : [before, original, duplicate, after];

        //Act
        if (useAsync)
            await outbox.AddAsync(batch, batchContext);
        else
            outbox.Add(batch, batchContext);

        //Assert
        var storedBefore = useAsync
            ? await outbox.GetAsync(before.Id, batchContext)
            : outbox.Get(before.Id, batchContext);
        var storedAfter = useAsync
            ? await outbox.GetAsync(after.Id, batchContext)
            : outbox.Get(after.Id, batchContext);
        var storedOriginal = useAsync
            ? await outbox.GetAsync(original.Id, batchContext)
            : outbox.Get(original.Id, batchContext);
        Assert.Equal(before.Id, storedBefore.Id);
        Assert.Equal(before.Body.Value, storedBefore.Body.Value);
        Assert.Equal(after.Id, storedAfter.Id);
        Assert.Equal(after.Body.Value, storedAfter.Body.Value);
        Assert.Equal(original.Id, storedOriginal.Id);
        Assert.Equal(original.Body.Value, storedOriginal.Body.Value);
        Assert.Equal(3L, store.Count());
        if (hasCausation)
        {
            Assert.Equal("batch-causation", store.ReadCausationId(before.Id));
            Assert.Equal("batch-causation", store.ReadCausationId(after.Id));
            Assert.Equal(alreadyStored ? "original-causation" : "batch-causation",
                store.ReadCausationId(original.Id));
        }
    }
}
