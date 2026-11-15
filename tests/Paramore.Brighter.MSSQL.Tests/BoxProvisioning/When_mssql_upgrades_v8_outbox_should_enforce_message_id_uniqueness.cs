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

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlMessageIdMigrationTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
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
        await Assert.That(await store.RowCountAsync()).IsEqualTo(1);
        await Assert.That(await store.MessageIdUniqueIndexesAsync() > 0).IsTrue();
        await Assert.That(await store.LatestVersionAsync()).IsEqualTo(9);
        var stored = await Assert.That(await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).HasSingleItem();
        await Assert.That(stored.Body.ToByteArray()).IsEqualTo(original.Body.ToByteArray());
        await Assert.That(await store.ScalarAsync($"SELECT COUNT(*) FROM [{store.Schema}].[__BrighterMigrationHistory]")).IsEqualTo(2);
    }
}
