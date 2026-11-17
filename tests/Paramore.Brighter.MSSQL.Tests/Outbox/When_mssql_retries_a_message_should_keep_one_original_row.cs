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
using System.Threading.Tasks;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

public class MsSqlMessageIdRetryTests
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
    public async Task When_mssql_retries_a_message_should_keep_one_original_row(bool binary, bool useAsync, bool customSchema)
    {
        //Arrange
        await using var store = new MsSqlMessageIdOutboxStore(binary, customSchema);
        await store.InitializeAsync();
        var outbox = new MsSqlOutbox(store.OutboxConfig);
        var context = new RequestContext();
        var original = MsSqlMessageIdOutboxStore.Message(Id.Random(), "original payload");
        var retry = MsSqlMessageIdOutboxStore.Message(original.Id, "retry payload");

        //Act
        if (useAsync)
        {
            await outbox.AddAsync(original, context);
            await outbox.AddAsync(retry, context);
        }
        else
        {
            outbox.Add(original, context);
            outbox.Add(retry, context);
        }

        //Assert
        Assert.Equal(1, await store.RowCountAsync());
        Assert.True(await store.MessageIdUniqueIndexesAsync() > 0);
        var outstanding = await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context);
        var stored = Assert.Single(outstanding);
        Assert.Equal(original.Id, stored.Id);
        Assert.Equal(original.Body.ToByteArray(), stored.Body.ToByteArray());
        Assert.Equal(1, await outbox.GetNumberOfOutstandingMessagesAsync(context));
    }
}
