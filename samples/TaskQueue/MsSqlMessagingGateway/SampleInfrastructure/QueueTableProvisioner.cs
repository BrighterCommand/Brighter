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
///
/// The existence check is not <see cref="MsSqlQueueBuilder.GetExistsQuery"/>, although that ships
/// too: it formats both names into the SQL and defaults the schema to a literal <c>dbo</c>, which
/// a login with a different default schema would not match. The version here binds the name as a
/// parameter and asks <c>SCHEMA_NAME()</c>.
/// </remarks>
public static class QueueTableProvisioner
{
    // SQL Server: "there is already an object named ...", and its index equivalent.
    private const int OBJECT_ALREADY_EXISTS = 2714;
    private const int INDEX_ALREADY_EXISTS = 1913;
    private const int DEADLOCK_VICTIM = 1205;

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

        // This runs before the host exists, so there is no logging to fail into: three of the four
        // applications would show a bare stack trace for the commonest first-run mistake.
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            CreateTable(connection, queueTableName);
            CreateIndex(connection, queueTableName);
        }
        catch (Exception ex) when (ex is SqlException or ArgumentException)
        {
            // ArgumentException as well as SqlException: a malformed connection string throws it
            // from the SqlConnection constructor, and that is as common a first-run mistake as an
            // unreachable server.
            throw new InvalidOperationException(
                $"Could not provision '{queueTableName}'. Check the connection string, that the " +
                "server is reachable, and that BrighterSqlQueue.sql has been run; set " +
                $"ConnectionStrings__Brighter to point somewhere else. The provider said: {ex.Message}", ex);
        }
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
        Execute(connection, sql, OBJECT_ALREADY_EXISTS, queueTableName);
    }

    // Guarded on what the index IS, not what it is called: any index whose leading column is
    // Topic on this table. A name guard would have to duplicate the convention MsSqlQueueBuilder
    // owns, and would then silently stop matching if that convention ever moved. The catch stays
    // as the race backstop it is described as.
    private static void CreateIndex(SqlConnection connection, string queueTableName)
    {
        var sql = $"""
                   IF NOT EXISTS (SELECT 1 FROM sys.indexes i
                                  INNER JOIN sys.index_columns ic
                                      ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                                  INNER JOIN sys.columns c
                                      ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                                  WHERE i.object_id = OBJECT_ID(QUOTENAME(SCHEMA_NAME()) + '.' + QUOTENAME(@queueTable))
                                    AND i.is_primary_key = 0
                                    AND ic.key_ordinal = 1
                                    AND c.name = 'Topic')
                   BEGIN
                       {MsSqlQueueBuilder.GetIndexDDL(queueTableName)}
                   END;
                   """;

        Execute(connection, sql, INDEX_ALREADY_EXISTS, queueTableName);
    }

    // All four applications share one database, so two starting together can both pass a guard and
    // race to create. Box Provisioning takes an advisory lock; there is no equivalent for the
    // queue, so the loser treats "already exists" as the outcome it wanted. A deadlock victim is
    // retried once — concurrent DDL surfaces as 1205 as readily as 2714 or 1913 — and a second
    // 1205 propagates, because a database deadlocking twice on one CREATE is not a race any more.
    private static void Execute(SqlConnection connection, string sql, int alreadyExists, string queueTableName)
    {
        try
        {
            ExecuteOnce(connection, sql, queueTableName);
        }
        catch (SqlException ex) when (ex.Number == DEADLOCK_VICTIM)
        {
            try
            {
                ExecuteOnce(connection, sql, queueTableName);
            }
            catch (SqlException retry) when (retry.Number == alreadyExists)
            {
                // The process we deadlocked with created it first.
            }
        }
        catch (SqlException ex) when (ex.Number == alreadyExists)
        {
            // Lost the create race; the other process made it, which is the outcome we wanted.
        }
    }

    // The parameter is built here rather than passed in, because SqlCommand.Dispose does not
    // detach parameters: a retry that re-used the instance would throw ArgumentException — "the
    // SqlParameter is already contained by another SqlParameterCollection" — out of the one path
    // that exists to survive a race.
    private static void ExecuteOnce(SqlConnection connection, string sql, string queueTableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@queueTable", queueTableName);
        command.ExecuteNonQuery();
    }
}
