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

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlMessageIdMigrationConflictTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
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
        var failure = await TestExceptionRecorder.CaptureAsync(() => store.ProvisionAsync());

        //Assert
        await Assert.That(failure).IsNotNull();
        await Assert.That(failure.ToString()).Contains("MessageId");
        await Assert.That(failure.ToString()).Contains("duplicate");
        await Assert.That(failure.ToString()).Contains(store.Table);
        await Assert.That(await store.LatestVersionAsync()).IsEqualTo(withHistory ? 8 : 0);
        await Assert.That(await store.RowCountAsync()).IsEqualTo(2);
        await Assert.That(await store.MessageIdUniqueIndexesAsync()).IsEqualTo(0);
        var preserved = (await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).ToArray();
        await Assert.That(preserved.Length).IsEqualTo(2);
        await Assert.That(System.Linq.Enumerable.Any(preserved, message => message.Body.Value == "first version")).IsTrue();
        await Assert.That(System.Linq.Enumerable.Any(preserved, message => message.Body.Value == "conflicting version")).IsTrue();

        //Arrange
        await store.ExecuteAsync($"DELETE FROM {store.QualifiedTable} WHERE [Id] = (SELECT MAX([Id]) FROM {store.QualifiedTable})");

        //Act
        await store.ProvisionAsync();
        await outbox.AddAsync(MsSqlMessageIdOutboxStore.Message(id, "retry"), context);

        //Assert
        await Assert.That(await store.RowCountAsync()).IsEqualTo(1);
        await Assert.That(await store.LatestVersionAsync()).IsEqualTo(9);
        await Assert.That(await store.MessageIdUniqueIndexesAsync() > 0).IsTrue();
    }
}
