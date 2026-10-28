#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel <guilhermeaappel@gmail.com>

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
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

/// <summary>
/// Builds the outbox and the held store for the cancellation case of the overall outstanding count: a database
/// file of its own in rollback-journal mode, held with an exclusive lock by a second connection until the setup
/// is disposed. Readers block while the lock is held, which WAL mode would not do.
/// </summary>
public static class HeldSqliteOutstandingCountStore
{
    public static async Task<OutstandingCountCancellationSetup<DbTransaction>> PrepareAsync(bool binaryMessagePayload)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"brighter_sqlite_held_count_{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={dbPath};Pooling=False";
        var tableName = $"held_outbox_{Guid.NewGuid():N}";

        await using (var setup = new SqliteConnection(connectionString))
        {
            await setup.OpenAsync();
            await ExecuteAsync(setup, "PRAGMA journal_mode=DELETE");
            await ExecuteAsync(setup, SqliteOutboxBuilder.GetDDL(tableName));
        }

        var holder = new SqliteConnection(connectionString);
        await holder.OpenAsync();
        await ExecuteAsync(holder, "BEGIN EXCLUSIVE");

        var outbox = new SqliteOutbox(
            new RelationalDatabaseConfiguration(connectionString, outBoxTableName: tableName, binaryMessagePayload: binaryMessagePayload));

        return new OutstandingCountCancellationSetup<DbTransaction>(
            outbox,
            storeHeld: true,
            clientCancellationException: typeof(SqliteException),
            release: async () =>
            {
                try
                {
                    await ExecuteAsync(holder, "ROLLBACK");
                }
                finally
                {
                    await holder.DisposeAsync();
                    SqliteConnection.ClearAllPools();
                    try
                    {
                        File.Delete(dbPath);
                    }
                    catch (IOException)
                    {
                    }
                }
            });
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
