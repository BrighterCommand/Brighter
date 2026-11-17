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
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlMessageIdMigrationConflictTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_mssql_migrates_duplicate_message_ids_should_preserve_rows_and_allow_retry_after_resolution(
        bool binary, bool withHistory)
    {
        //Arrange
        await using var store = new MsSqlMessageIdOutboxStore(binary, customSchema: true);
        await store.InitializeAsync(legacy: true, withHistory: withHistory);
        var outbox = new MsSqlOutbox(store.OutboxConfig);
        var context = new RequestContext();
        var id = Id.Random();
        await outbox.AddAsync(MsSqlMessageIdOutboxStore.Message(id, "first version"), context);
        await outbox.AddAsync(MsSqlMessageIdOutboxStore.Message(id, "conflicting version"), context);

        //Act
        var failure = await Record.ExceptionAsync(() => store.ProvisionAsync());

        //Assert
        Assert.NotNull(failure);
        Assert.Contains("MessageId", failure.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicate", failure.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(store.Table, failure.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(withHistory ? 8 : 0, await store.LatestVersionAsync());
        Assert.Equal(2, await store.RowCountAsync());
        Assert.Equal(0, await store.MessageIdUniqueIndexesAsync());
        var preserved = (await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).ToArray();
        Assert.Equal(2, preserved.Length);
        Assert.Contains(preserved, message => message.Body.Value == "first version");
        Assert.Contains(preserved, message => message.Body.Value == "conflicting version");

        //Arrange
        await store.ExecuteAsync($"DELETE FROM {store.QualifiedTable} WHERE [Id] = (SELECT MAX([Id]) FROM {store.QualifiedTable})");

        //Act
        await store.ProvisionAsync();
        await outbox.AddAsync(MsSqlMessageIdOutboxStore.Message(id, "retry"), context);

        //Assert
        Assert.Equal(1, await store.RowCountAsync());
        Assert.Equal(9, await store.LatestVersionAsync());
        Assert.True(await store.MessageIdUniqueIndexesAsync() > 0);
    }
}
