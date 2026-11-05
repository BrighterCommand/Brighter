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

using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

public class MsSqlMessageIdConcurrentRetryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_mssql_concurrent_adds_share_a_message_id_should_store_one_row(bool binary)
    {
        //Arrange
        await using var store = new MsSqlMessageIdOutboxStore(binary);
        await store.InitializeAsync();
        var id = Id.Random();
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = Enumerable.Range(0, 8).Select(async _ =>
        {
            await start.Task;
            await new MsSqlOutbox(store.OutboxConfig).AddAsync(
                MsSqlMessageIdOutboxStore.Message(id, "same message"), new RequestContext());
        }).ToArray();

        //Act
        start.SetResult(true);
        await Task.WhenAll(writes);

        //Assert
        Assert.Equal(1, await store.RowCountAsync());
    }
}
