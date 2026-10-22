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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

/// <summary>
/// A SQLite outbox on its own database file, in rollback-journal mode, holding one outstanding message,
/// with an exclusive lock that a second connection takes and releases after a chosen time. Readers
/// block while the lock is held, which WAL mode would not do.
/// </summary>
public sealed class LockedSqliteOutbox : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_locked_outbox_{Guid.NewGuid():N}.db");
    private readonly string _connectionString;
    private SqliteConnection? _lockHolder;
    private Task? _release;

    /// <summary>
    /// Builds the outbox from a <see cref="RelationalDatabaseConfiguration"/>.
    /// </summary>
    /// <param name="outstandingCountTimeout">The outstanding count timeout to configure, or <c>null</c> for the default.</param>
    public LockedSqliteOutbox(TimeSpan? outstandingCountTimeout = null)
        : this((connectionString, tableName) => new RelationalDatabaseConfiguration(
            connectionString, outBoxTableName: tableName, outstandingCountTimeout: outstandingCountTimeout))
    {
    }

    /// <summary>
    /// Builds the outbox from the configuration that <paramref name="configure"/> returns for the database's
    /// connection string and outbox table name.
    /// </summary>
    /// <param name="configure">Returns the configuration, given the connection string and then the outbox table name.</param>
    public LockedSqliteOutbox(Func<string, string, IAmARelationalDatabaseConfiguration> configure)
    {
        _connectionString = $"Data Source={_dbPath};Pooling=False";
        var tableName = $"locked_outbox_{Guid.NewGuid():N}";

        using (var setup = new SqliteConnection(_connectionString))
        {
            setup.Open();
            Execute(setup, "PRAGMA journal_mode=DELETE");
            Execute(setup, SqliteOutboxBuilder.GetDDL(tableName));
        }

        Outbox = new SqliteOutbox(configure(_connectionString, tableName));
        Outbox.Add(new DefaultMessageFactory().Create(DateTimeOffset.UtcNow.AddMinutes(-5)), new RequestContext());
    }

    public SqliteOutbox Outbox { get; }

    /// <summary>
    /// Takes the exclusive lock now and releases it after <paramref name="holdFor"/>, on another thread.
    /// </summary>
    public void LockFor(TimeSpan holdFor)
    {
        _lockHolder = new SqliteConnection(_connectionString);
        _lockHolder.Open();
        Execute(_lockHolder, "BEGIN EXCLUSIVE");
        _release = Task.Run(() =>
        {
            Thread.Sleep(holdFor);
            Execute(_lockHolder, "ROLLBACK");
        });
    }

    public void Dispose()
    {
        _release?.GetAwaiter().GetResult();
        _lockHolder?.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
