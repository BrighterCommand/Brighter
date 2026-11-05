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

#nullable enable

using System;
using Npgsql;
using Paramore.Brighter.Outbox.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

internal sealed class PostgreSqlOutboxTestStore : IDisposable
{
    public string TableName { get; } = $"bulk_{Uuid.New():N}";

    public RelationalDatabaseConfiguration Configuration { get; }

    public PostgreSqlOutboxTestStore(bool binaryPayload, bool hasCausation)
    {
        Configuration = new RelationalDatabaseConfiguration(
            Const.ConnectionString,
            databaseName: "brightertests",
            outBoxTableName: TableName,
            binaryMessagePayload: binaryPayload);
        Execute(PostgreSqlOutboxBuilder.GetDDL(TableName, binaryPayload));
        if (!hasCausation)
            Execute($"ALTER TABLE \"{TableName}\" DROP COLUMN causationid");
    }

    public void Execute(string sql)
    {
        using var connection = new NpgsqlConnection(Const.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public long Count()
    {
        using var connection = new NpgsqlConnection(Const.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{TableName}\"";
        return (long)command.ExecuteScalar()!;
    }

    public string? ReadCausationId(Id messageId)
    {
        using var connection = new NpgsqlConnection(Const.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT causationid FROM \"{TableName}\" WHERE messageid = @id";
        command.Parameters.AddWithValue("id", messageId.Value);
        return command.ExecuteScalar() as string;
    }

    public static Message MessageWith(Id id, string body, string topic = "bulk-topic")
        => new(new MessageHeader(id, new RoutingKey(topic), MessageType.MT_DOCUMENT),
            new MessageBody(body));

    public void Dispose() => Execute($"DROP TABLE IF EXISTS \"{TableName}\"");
}
