#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia

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
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.TestHelpers;

internal sealed class SqliteConstraintDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteConstraintDatabase(bool inbox, bool binary, string constraint)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"constraint_{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            ForeignKeys = true
        }.ToString();
        Configuration = new RelationalDatabaseConfiguration(connectionString,
            inboxTableName: "Messages", outBoxTableName: "Messages", binaryMessagePayload: binary);
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        try
        {
            Execute("CREATE TABLE AllowedValues (Value TEXT PRIMARY KEY);");
            var ddl = inbox
                ? SqliteInboxBuilder.GetDDL("Messages", binary)
                : SqliteOutboxBuilder.GetDDL("Messages", binary);
            if (constraint is "PRIMARY KEY" or "UNIQUE")
            {
                ddl = inbox
                    ? ddl.Replace("CONSTRAINT PK_MessageId PRIMARY KEY", constraint)
                    : ddl.Replace("[MessageId] TEXT NOT NULL COLLATE NOCASE PRIMARY KEY",
                        $"[MessageId] TEXT NOT NULL COLLATE NOCASE {constraint}");
            }
            else
            {
                var column = constraint switch
                {
                    "CHECK" => "TEXT DEFAULT 'rejected' CHECK (ConstraintGuard <> 'rejected')",
                    "NOT NULL" => "TEXT NOT NULL",
                    "FOREIGN KEY" => "TEXT DEFAULT 'rejected' REFERENCES AllowedValues(Value)",
                    "TRIGGER" => null,
                    _ => throw new ArgumentOutOfRangeException(nameof(constraint))
                };
                if (column is not null)
                    ddl = ddl.Insert(ddl.IndexOf('(') + 1, $"\n[ConstraintGuard] {column},");
            }

            Execute(ddl);
            if (constraint == "TRIGGER")
                Execute("""
                    CREATE TRIGGER RejectWrite BEFORE INSERT ON Messages
                    BEGIN
                        SELECT RAISE(ABORT, 'Rejected write');
                    END;
                    """);
        }
        catch
        {
            _connection.Dispose();
            throw;
        }
    }

    public RelationalDatabaseConfiguration Configuration { get; }

    public long RowCount()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Messages;";
        return (long)command.ExecuteScalar()!;
    }

    public void Dispose() => _connection.Dispose();

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
