#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-52 - an opted-in handler's outbox write shares the controller's DbContext, and therefore its
// transaction, and rolls back with it. A controller opens a transaction on its own DbContext, writes an
// entity, then Sends a command whose handler deposits a message into the outbox over the same,
// ambient-adopted DbContext - never committing or rolling back anything itself. Three facts: the
// committing run proves both writes are visible to one another before the commit, and both survive it;
// the rollback re-run proves the two writes are genuinely one transaction, not merely one instance,
// because rolling back undoes both together; the negative control, under AlwaysNew, pins today's
// behaviour - the handler's own, unrelated write is never rolled back with the controller's, because it
// was never part of the same transaction to begin with, and nothing here is reported as a warning or an
// error, silently.
public class DepositTransactionSharesOutboxWriteTests
{
    [Fact]
    public async Task When_a_handler_deposits_in_an_opted_in_request_the_outbox_write_should_join_the_caller_transaction()
    {
        // Arrange
        await using var factory = new DepositTransactionWebApplicationFactory(ScopeAffinity.JoinAmbient);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/deposit-transactions?rollback=false", content: null);

        // Assert - the request completed, and the handler resolved the controller's own DbContext
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<DepositTransactionRecorder>();
        Assert.True(recorder.HandlerSharesControllerDbContext);

        // Assert - before the commit, the controller's own connection could already see both writes
        Assert.True(recorder.EntityVisibleMidTransaction);
        Assert.True(recorder.OutboxRowVisibleMidTransaction);

        // Assert - after the commit, both writes are durably present
        Assert.Equal(1, CountRows(factory.ConnectionString, "DepositedEntities"));
        Assert.Equal(1, CountRows(factory.OutboxConnectionString, DepositTransactionWebApplicationFactory.OutboxTableName));
    }

    [Fact]
    public async Task When_the_controller_rolls_back_neither_the_entity_nor_the_outbox_row_should_be_present()
    {
        // Arrange
        await using var factory = new DepositTransactionWebApplicationFactory(ScopeAffinity.JoinAmbient);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/deposit-transactions?rollback=true", content: null);

        // Assert - the request completed, and the handler resolved the controller's own DbContext
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<DepositTransactionRecorder>();
        Assert.True(recorder.HandlerSharesControllerDbContext);

        // Assert - the rollback undid both writes together, proving one shared transaction rather than
        // merely one shared instance
        Assert.Equal(0, CountRows(factory.ConnectionString, "DepositedEntities"));
        Assert.Equal(0, CountRows(factory.OutboxConnectionString, DepositTransactionWebApplicationFactory.OutboxTableName));
    }

    [Fact]
    public async Task When_the_host_uses_always_new_the_handlers_outbox_write_should_not_roll_back_with_the_controllers()
    {
        // Arrange - the negative control: the extension's own affinity argument is AlwaysNew, never
        // assigned on the options object, pinning today's (pre-adoption) behaviour
        await using var factory = new DepositTransactionWebApplicationFactory(ScopeAffinity.AlwaysNew);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/deposit-transactions?rollback=true", content: null);

        // Assert - the handler resolved its own, distinct DbContext, not the controller's
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<DepositTransactionRecorder>();
        Assert.False(recorder.HandlerSharesControllerDbContext);

        // Assert - the controller's own entity write rolled back, but the handler's unrelated outbox
        // write, made outside that transaction, survives
        Assert.Equal(0, CountRows(factory.ConnectionString, "DepositedEntities"));
        Assert.Equal(1, CountRows(factory.OutboxConnectionString, DepositTransactionWebApplicationFactory.OutboxTableName));

        // Assert - none of this was reported as a warning or an error; C-21's silence is present
        // behaviour, not something an implementation could satisfy merely by logging it
        var loggerProvider = factory.Services.GetRequiredService<CapturingLoggerProvider>();
        Assert.DoesNotContain(loggerProvider.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    private static int CountRows(string connectionString, string tableName)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName}";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
