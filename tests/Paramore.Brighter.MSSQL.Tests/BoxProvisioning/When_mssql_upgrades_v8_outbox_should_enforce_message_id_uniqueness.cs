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

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlMessageIdMigrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_mssql_upgrades_v8_outbox_should_enforce_message_id_uniqueness(bool binary, bool withHistory)
    {
        //Arrange
        await using var store = new MsSqlMessageIdOutboxStore(binary, customSchema: true);
        await store.InitializeAsync(legacy: true, withHistory: withHistory);
        var outbox = new MsSqlOutbox(store.OutboxConfig);
        var context = new RequestContext();
        var original = MsSqlMessageIdOutboxStore.Message(Id.Random(), "preserve existing row");
        await outbox.AddAsync(original, context);

        //Act
        await store.ProvisionAsync();
        await store.ProvisionAsync();
        await outbox.AddAsync(MsSqlMessageIdOutboxStore.Message(original.Id, "retry"), context);

        //Assert
        Assert.Equal(1, await store.RowCountAsync());
        Assert.True(await store.MessageIdUniqueIndexesAsync() > 0);
        Assert.Equal(9, await store.LatestVersionAsync());
        var stored = Assert.Single(await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context));
        Assert.Equal(original.Body.ToByteArray(), stored.Body.ToByteArray());
        Assert.Equal(2, await store.ScalarAsync($"SELECT COUNT(*) FROM [{store.Schema}].[__BrighterMigrationHistory]"));
    }
}
