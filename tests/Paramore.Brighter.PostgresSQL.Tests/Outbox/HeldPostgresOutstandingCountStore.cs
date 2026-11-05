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
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

/// <summary>
/// Builds the outbox and the held store for the cancellation case of the overall outstanding count: a table of
/// its own, held with an access exclusive lock by a second connection until the setup is disposed or the hold time has passed. A plain read
/// blocks on that lock.
/// </summary>
public static class HeldPostgresOutstandingCountStore
{
    public static async Task<OutstandingCountCancellationSetup<DbTransaction>> PrepareAsync(bool binaryMessagePayload, TimeSpan holdFor)
    {
        var configuration = new RelationalDatabaseConfiguration(Const.ConnectionString,
            databaseName: "brightertests",
            outBoxTableName: $"Held{Uuid.New():N}",
            binaryMessagePayload: binaryMessagePayload);

        await using (var setup = new NpgsqlConnection(configuration.ConnectionString))
        {
            await setup.OpenAsync();
            await ExecuteAsync(setup, PostgreSqlOutboxBuilder.GetDDL(configuration.OutBoxTableName));
        }

        var holder = new NpgsqlConnection(configuration.ConnectionString);
        await holder.OpenAsync();
        var transaction = await holder.BeginTransactionAsync();
        await ExecuteAsync(holder, $"LOCK TABLE {configuration.OutBoxTableName} IN ACCESS EXCLUSIVE MODE", transaction);

        return new OutstandingCountCancellationSetup<DbTransaction>(
            new PostgreSqlOutbox(configuration),
            storeHeld: true,
            clientCancellationException: typeof(PostgresException),
            release: async () =>
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                finally
                {
                    await transaction.DisposeAsync();
                    await holder.DisposeAsync();
                    await using var cleanup = new NpgsqlConnection(configuration.ConnectionString);
                    await cleanup.OpenAsync();
                    await ExecuteAsync(cleanup, $"DROP TABLE {configuration.OutBoxTableName}");
                }
            },
            holdFor);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync();
    }
}
