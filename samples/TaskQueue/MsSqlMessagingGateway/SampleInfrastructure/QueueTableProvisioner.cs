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

using Microsoft.Data.SqlClient;
using Paramore.Brighter.MessagingGateway.MsSql;

namespace SampleInfrastructure;

/// <summary>
/// Creates the queue table and its index if either is missing.
/// </summary>
/// <remarks>
/// Box Provisioning covers the Outbox and the Inbox, but nothing covers the queue: the MSSQL
/// gateway has no provisioning path at all, so <c>OnMissingChannel.Create</c> is accepted on a
/// publication or subscription and then never acted on. Without this, the first thing any of
/// these applications does is fail with <c>Invalid object name 'QueueData'</c> from inside the
/// pump.
///
/// The DDL is Brighter's own, from <see cref="MsSqlQueueBuilder"/> — which is public precisely
/// because callers have to run it themselves.
/// </remarks>
public static class QueueTableProvisioner
{
    // 2714 = "There is already an object named '...'", 1913 = the index equivalent. All four
    // applications run this against one shared database, so two starting together can both pass
    // the IF NOT EXISTS and race to CREATE. Box Provisioning avoids this with an advisory lock;
    // there is no equivalent for the queue, so the loser is caught and treated as success.
    private const int ObjectAlreadyExists = 2714;
    private const int IndexAlreadyExists = 1913;

    /// <summary>
    /// Creates the queue table and its topic index if they are absent, and does nothing if they
    /// are present. Safe to run on every start, and safe to run concurrently.
    /// </summary>
    public static void EnsureQueueTable(string connectionString, string queueTableName)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        // One batch rather than a read followed by a write, and each object guarded separately
        // so a table that exists without its index still gets one. The guards test
        // SCHEMA_NAME() rather than a literal 'dbo': MsSqlQueueBuilder.GetDDL emits an
        // unqualified CREATE TABLE, which lands in the caller's default schema, so a login whose
        // default is not dbo would otherwise never match its own table and would re-attempt the
        // create on every start. (MsSqlQueueBuilder.GetExistsQuery defaults to dbo for the same
        // reason it cannot know better.)
        //
        // The catalog lookups take the names as PARAMETERS. Here they come from a constant, so
        // there is nothing to inject — but a sample is a copy-paste source, and a reader who
        // wires the table name to configuration would otherwise inherit an injection point in
        // code holding DDL rights. Only the DDL itself interpolates, because an object name
        // cannot be a parameter, and that DDL is Brighter's own. MsSqlBoxMigrationRunner:244
        // parameterizes the same kind of catalog check.
        var sql = $"""
                   IF NOT EXISTS (SELECT 1 FROM sys.tables t
                                  INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                                  WHERE t.name = @queueTable AND s.name = SCHEMA_NAME())
                   BEGIN
                       {MsSqlQueueBuilder.GetDDL(queueTableName)}
                   END;

                   IF NOT EXISTS (SELECT 1 FROM sys.indexes
                                  WHERE name = @indexName
                                    AND object_id = OBJECT_ID(QUOTENAME(SCHEMA_NAME()) + '.' + QUOTENAME(@queueTable)))
                   BEGIN
                       {MsSqlQueueBuilder.GetIndexDDL(queueTableName)}
                   END;
                   """;

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@queueTable", queueTableName);
            command.Parameters.AddWithValue("@indexName", IndexName(queueTableName));
            command.ExecuteNonQuery();
        }
        catch (SqlException ex) when (ex.Number is ObjectAlreadyExists or IndexAlreadyExists)
        {
            // Another instance won the race and created it between our check and our create.
            // That is the outcome we wanted anyway.
        }
    }

    /// <summary>
    /// The name <see cref="MsSqlQueueBuilder.GetIndexDDL"/> gives the topic index.
    /// </summary>
    /// <remarks>
    /// This duplicates a convention that lives in Brighter — <c>QUEUE_TABLE_INDEX_DDL</c> in
    /// <c>MsSqlQueueBuilder</c>, which formats <c>IX_{0}_Topic</c> — and the builder exposes no
    /// <c>GetIndexName</c> to ask instead. That duplication fails SILENTLY if the convention ever
    /// changes: the guard stops matching, every start re-attempts the CREATE INDEX, it fails with
    /// 1913, the catch above swallows it, and a permanently failing statement looks exactly like
    /// success. It is here as a named method rather than inline so there is one place to change,
    /// and so the dependency on Brighter's naming is stated rather than buried in a string.
    /// </remarks>
    private static string IndexName(string queueTableName) => $"IX_{queueTableName}_Topic";
}
