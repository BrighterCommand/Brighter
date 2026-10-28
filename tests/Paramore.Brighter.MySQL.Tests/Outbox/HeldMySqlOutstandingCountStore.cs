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

using System.Data.Common;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Outbox.MySql;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

/// <summary>
/// Builds the outbox and the held store for the cancellation case of the overall outstanding count: a table of
/// its own, held with a write lock by a second connection until the setup is disposed. A plain read
/// blocks on that lock.
/// </summary>
public static class HeldMySqlOutstandingCountStore
{
    public static async Task<OutstandingCountCancellationSetup<DbTransaction>> PrepareAsync(bool binaryMessagePayload)
    {
        var configuration = new RelationalDatabaseConfiguration(Const.DefaultConnectingString,
            databaseName: "brightertests",
            outBoxTableName: $"held_{Uuid.New():N}",
            binaryMessagePayload: binaryMessagePayload);

        await using (var setup = new MySqlConnection(configuration.ConnectionString))
        {
            await setup.OpenAsync();
            await ExecuteAsync(setup, MySqlOutboxBuilder.GetDDL(configuration.OutBoxTableName));
        }

        var holder = new MySqlConnection(configuration.ConnectionString);
        await holder.OpenAsync();
        await ExecuteAsync(holder, $"LOCK TABLES {configuration.OutBoxTableName} WRITE");

        return new OutstandingCountCancellationSetup<DbTransaction>(
            new MySqlOutbox(configuration),
            storeHeld: true,
            clientCancellationException: typeof(MySqlException),
            release: async () =>
            {
                try
                {
                    await ExecuteAsync(holder, "UNLOCK TABLES");
                }
                finally
                {
                    await holder.DisposeAsync();
                    await using var cleanup = new MySqlConnection(configuration.ConnectionString);
                    await cleanup.OpenAsync();
                    await ExecuteAsync(cleanup, $"DROP TABLE {configuration.OutBoxTableName}");
                }
            });
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
