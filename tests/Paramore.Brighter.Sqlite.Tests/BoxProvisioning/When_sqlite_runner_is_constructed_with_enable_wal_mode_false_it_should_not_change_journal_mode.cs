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

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class When_sqlite_runner_is_constructed_with_enable_wal_mode_false_it_should_not_change_journal_mode : IAsyncLifetime
{
    // PRAGMA journal_mode is database-file-wide and persistent (the choice survives connection
    // close). A host application that has deliberately picked DELETE or TRUNCATE journal mode
    // does not want the box-provisioning runner silently switching its file to WAL on startup.
    // The opt-out flag enableWalMode=false skips the pragma entirely so the existing journal
    // mode is preserved. This Fact pins that contract.
    //
    // We use an isolated file per test (not the shared test.db) so the assertion observes
    // exactly the runner's effect and isn't polluted by other tests already in WAL mode.

    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), $"brighter_wal_optout_{Guid.NewGuid():N}.db");

    private string ConnectionString => $"DataSource=\"{_databasePath}\"";

    [Fact]
    public async Task Should_preserve_existing_DELETE_journal_mode_when_wal_mode_disabled()
    {
        //Arrange — start the database in DELETE journal mode and create an empty placeholder
        //          table so the file persists between connections.
        await SetJournalModeAsync(ConnectionString, "DELETE");
        Assert.Equal("delete", await GetJournalModeAsync(ConnectionString));

        var tableName = $"test_outbox_{Guid.NewGuid():N}";
        var config = new RelationalDatabaseConfiguration(
            ConnectionString, outBoxTableName: tableName);
        var runner = new SqliteBoxMigrationRunner(
            config, TimeSpan.FromSeconds(30), enableWalMode: false);

        //Act — let the runner provision a fresh outbox.
        var migrations = new SqliteOutboxMigrationCatalog().All(config);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);
        await runner.MigrateAsync(tableName, schemaName: null, BoxType.Outbox, migrations, freshHint);

        //Assert — journal mode is still DELETE (the runner did not run the WAL pragma).
        Assert.Equal("delete", await GetJournalModeAsync(ConnectionString));
    }

    [Fact]
    public async Task Should_switch_to_WAL_journal_mode_when_wal_mode_enabled()
    {
        //Arrange — start in DELETE so the assertion observes the runner's effect, not a
        //          pre-existing state.
        await SetJournalModeAsync(ConnectionString, "DELETE");
        Assert.Equal("delete", await GetJournalModeAsync(ConnectionString));

        var tableName = $"test_outbox_{Guid.NewGuid():N}";
        var config = new RelationalDatabaseConfiguration(
            ConnectionString, outBoxTableName: tableName);
        var runner = new SqliteBoxMigrationRunner(
            config, TimeSpan.FromSeconds(30), enableWalMode: true);

        //Act
        var migrations = new SqliteOutboxMigrationCatalog().All(config);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);
        await runner.MigrateAsync(tableName, schemaName: null, BoxType.Outbox, migrations, freshHint);

        //Assert — journal mode is now WAL.
        Assert.Equal("wal", await GetJournalModeAsync(ConnectionString));
    }

    private static async Task SetJournalModeAsync(string connectionString, string mode)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA journal_mode={mode};";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> GetJournalModeAsync(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        var raw = await command.ExecuteScalarAsync();
        return ((string)raw!).ToLowerInvariant();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        // Best-effort cleanup of the per-test database file plus any WAL/SHM sidecars.
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(_databasePath + suffix); } catch { }
        }
        return Task.CompletedTask;
    }
}
