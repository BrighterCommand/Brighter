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

using System.Threading.Tasks;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlMessageIdFreshProvisioningTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_mssql_provisions_a_new_outbox_should_enforce_message_id_uniqueness(bool binary)
    {
        //Arrange
        await using var store = new MsSqlMessageIdOutboxStore(binary, customSchema: true);
        await store.InitializeAsync(provision: true);
        var outbox = new MsSqlOutbox(store.OutboxConfig);
        var message = MsSqlMessageIdOutboxStore.Message(Id.Random(), "original");
        var context = new RequestContext();

        //Act
        await outbox.AddAsync(message, context);
        await outbox.AddAsync(message, context);
        await store.ProvisionAsync();

        //Assert
        Assert.Equal(1, await store.RowCountAsync());
        Assert.True(await store.MessageIdUniqueIndexesAsync() > 0);
        Assert.Equal(9, await store.LatestVersionAsync());
        Assert.Equal(1, await store.ScalarAsync($"SELECT COUNT(*) FROM [{store.Schema}].[__BrighterMigrationHistory]"));
    }
}
