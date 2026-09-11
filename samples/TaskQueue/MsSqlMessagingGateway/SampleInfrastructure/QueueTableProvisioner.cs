#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.MessagingGateway.MsSql;

namespace SampleInfrastructure;

/// <summary>
/// Creates the queue table and its topic index if either is missing.
/// </summary>
/// <remarks>
/// Box Provisioning covers the Outbox and the Inbox; nothing covers the queue, because the MSSQL
/// gateway has no provisioning path — <c>OnMissingChannel.Create</c> is accepted and then never
/// acted on. The DDL is Brighter's own, from <see cref="MsSqlQueueBuilder"/>, which is public so
/// that callers can run it.
/// </remarks>
public static class QueueTableProvisioner
{
    // SQL Server: "there is already an object named ...", and its index equivalent.
    private const int OBJECT_ALREADY_EXISTS = 2714;
    private const int INDEX_ALREADY_EXISTS = 1913;

    private static readonly Regex s_identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Creates the queue table and its topic index if they are absent. Safe to run on every start,
    /// and safe to run concurrently.
    /// </summary>
    /// <param name="connectionString">The database holding the queue table.</param>
    /// <param name="queueTableName">The queue table, which must be a plain SQL identifier.</param>
    public static void EnsureQueueTable(string connectionString, string queueTableName)
    {
        // GetDDL formats the name into CREATE TABLE [{0}], where a ']' would close the bracket.
        if (!s_identifier.IsMatch(queueTableName))
            throw new ArgumentException($"'{queueTableName}' is not a plain SQL identifier", nameof(queueTableName));

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        CreateTable(connection, queueTableName);
        CreateIndex(connection, queueTableName);
    }

    // Two commands rather than one batch: a swallowed error abandons the rest of its batch, so a
    // table create that loses the race would take the index statement down with it.
    private static void CreateTable(SqlConnection connection, string queueTableName)
    {
        // SCHEMA_NAME() rather than a literal 'dbo': GetDDL emits an unqualified CREATE TABLE, so
        // a login whose default schema is not dbo would never match its own table.
        var sql = $"""
                   IF NOT EXISTS (SELECT 1 FROM sys.tables t
                                  INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                                  WHERE t.name = @queueTable AND s.name = SCHEMA_NAME())
                   BEGIN
                       {MsSqlQueueBuilder.GetDDL(queueTableName)}
                   END;
                   """;

        // The lookup is parameterized although the name here is a constant, because a sample is a
        // copy-paste source and the reader who binds it to configuration inherits this code.
        Execute(connection, sql, OBJECT_ALREADY_EXISTS, new SqlParameter("@queueTable", queueTableName));
    }

    // Unguarded, because the only way to guard it is to duplicate the index name that
    // MsSqlQueueBuilder owns — and a guard that silently stops matching leaves a permanently
    // failing statement looking like success. The cost is a caught 1913 on every start after the
    // first, which is deliberate: do not "fix" it by adding a guard.
    private static void CreateIndex(SqlConnection connection, string queueTableName) =>
        Execute(connection, MsSqlQueueBuilder.GetIndexDDL(queueTableName), INDEX_ALREADY_EXISTS);

    // All four applications share one database, so two starting together can both pass a guard and
    // race to create. Box Provisioning takes an advisory lock; there is no equivalent for the
    // queue, so the loser treats "already exists" as the outcome it wanted.
    private static void Execute(SqlConnection connection, string sql, int alreadyExists, params SqlParameter[] parameters)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
            command.ExecuteNonQuery();
        }
        catch (SqlException ex) when (ex.Number == alreadyExists)
        {
        }
    }
}
