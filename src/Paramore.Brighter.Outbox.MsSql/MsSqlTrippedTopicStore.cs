#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Outbox.MsSql;

/// <summary>
/// Shares tripped topics between instances through a table in the outbox's SQL Server database.
/// </summary>
/// <remarks>
/// The store keeps one row per topic, keeps the later of the stored and written stamps, and marks a row ended rather than
/// deleting it, so the application's database user needs no <c>DELETE</c> permission.
/// </remarks>
public class MsSqlTrippedTopicStore : RelationDatabaseTrippedTopicStore
{
    private const string DEFAULT_TABLE_NAME = "BrighterTrippedTopics";
    private const int DEADLOCK_VICTIM_ERROR_NUMBER = 1205;

    /// <summary>
    /// Creates a store over the trip table.
    /// </summary>
    /// <param name="connectionProvider">Provides the connection to the database that holds the trip table.</param>
    /// <param name="tableName">The name of the trip table, at most 107 characters so that the index name <c>idx_{tableName}_EndedAt_TripTime</c> fits SQL Server's 128-character limit.</param>
    /// <param name="schemaName">The schema of the trip table, or null for the connection's default schema.</param>
    /// <param name="commandTimeout">
    /// How long a command may run, rounded up to whole seconds with a minimum of one. Null means 2 seconds, the default store timeout.
    /// </param>
    public MsSqlTrippedTopicStore(
        IAmARelationalDbConnectionProvider connectionProvider,
        string tableName = DEFAULT_TABLE_NAME,
        string? schemaName = null,
        TimeSpan? commandTimeout = null)
        : base(connectionProvider, QualifiedTable(tableName, schemaName), commandTimeout)
    {
    }

    /// <summary>
    /// Creates a store over the trip table, taking the schema from the configuration the table was provisioned with.
    /// </summary>
    /// <param name="configuration">Supplies the schema of the trip table when <paramref name="schemaName"/> is null.</param>
    /// <param name="connectionProvider">Provides the connection to the database that holds the trip table.</param>
    /// <param name="tableName">The name of the trip table, at most 107 characters so that the index name <c>idx_{tableName}_EndedAt_TripTime</c> fits SQL Server's 128-character limit.</param>
    /// <param name="schemaName">The schema of the trip table, or null to use the configuration's schema.</param>
    /// <param name="commandTimeout">
    /// How long a command may run, rounded up to whole seconds with a minimum of one. Null means 2 seconds, the default store timeout.
    /// </param>
    public MsSqlTrippedTopicStore(
        IAmARelationalDatabaseConfiguration configuration,
        IAmARelationalDbConnectionProvider connectionProvider,
        string tableName = DEFAULT_TABLE_NAME,
        string? schemaName = null,
        TimeSpan? commandTimeout = null)
        : this(connectionProvider, tableName, schemaName ?? configuration.SchemaName, commandTimeout)
    {
    }

    /// <summary>
    /// Gets the SQL that deletes the ended rows older than a cutoff, for an operator to run under an account that may delete.
    /// </summary>
    /// <remarks>
    /// Brighter never runs this. Bind the parameter <c>@EndedBefore</c> to a UTC timestamp: only rows marked ended before it
    /// are deleted, and an unmarked row is never deleted.
    /// </remarks>
    /// <param name="tableName">The name of the trip table, at most 107 characters so that the index name <c>idx_{tableName}_EndedAt_TripTime</c> fits SQL Server's 128-character limit.</param>
    /// <param name="schemaName">The schema of the trip table, or null for the connection's default schema.</param>
    /// <returns>The parameterised DELETE.</returns>
    public static string GetPurgeSql(string tableName, string? schemaName = null) =>
        $"DELETE FROM {QualifiedTable(tableName, schemaName)} WHERE [EndedAt] IS NOT NULL AND [EndedAt] < @EndedBefore;";

    /// <summary>
    /// Gets the SQL that creates the trip table and the secondary index that serves the read's ranged update.
    /// </summary>
    /// <param name="tableName">The name of the trip table, at most 107 characters so that the index name <c>idx_{tableName}_EndedAt_TripTime</c> fits SQL Server's 128-character limit.</param>
    /// <param name="schemaName">The schema of the trip table, or null for the connection's default schema.</param>
    /// <returns>The DDL, safe to run more than once.</returns>
    public static string GetDDL(string tableName, string? schemaName = null)
    {
        var qualifiedTable = QualifiedTable(tableName, schemaName);
        var objectId = schemaName is null ? $"[{tableName}]" : $"[{schemaName}].[{tableName}]";

        return $"""
                IF OBJECT_ID(N'{objectId}', N'U') IS NULL
                    CREATE TABLE {qualifiedTable}(
                        [Topic] NVARCHAR(255) NOT NULL,
                        [TripTime] DATETIMEOFFSET(7) NOT NULL,
                        [PriorTripTime] DATETIMEOFFSET(7) NULL,
                        [EndedAt] DATETIMEOFFSET(7) NULL,
                        CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED ([Topic] ASC)
                    );
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'idx_{tableName}_EndedAt_TripTime' AND [object_id] = OBJECT_ID(N'{objectId}'))
                    EXEC('CREATE INDEX [idx_{tableName}_EndedAt_TripTime] ON {qualifiedTable} ([EndedAt], [TripTime]);');
                """;
    }

    /// <inheritdoc />
    protected override bool IsDeadlock(Exception exception) => exception is SqlException { Number: DEADLOCK_VICTIM_ERROR_NUMBER };

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<TopicTrip>> EndExpiredOnceAsync(
        TripWindow window,
        CancellationToken cancellationToken)
    {
        using var connection = await ConnectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await QueryTripsAsync(
            connection, null,
            $"""
            UPDATE {QualifiedTableName} SET [EndedAt] = @now
            OUTPUT INSERTED.[Topic], INSERTED.[TripTime]
            WHERE [EndedAt] IS NULL AND [TripTime] <= @cutoff;
            """,
            [("@now", window.Now), ("@cutoff", window.Cutoff)], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<TopicTrip>> ReadLiveOnceAsync(
        TripWindow window,
        CancellationToken cancellationToken)
    {
        using var connection = await ConnectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await QueryTripsAsync(
            connection, null,
            $"SELECT [Topic], [TripTime] FROM {QualifiedTableName} WHERE [EndedAt] IS NULL AND [TripTime] > @cutoff;",
            [("@cutoff", window.Cutoff)], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<TripWriteResult>> WriteOnceAsync(
        IReadOnlyList<TopicTrip> trips,
        CancellationToken cancellationToken)
    {
        var inOrdinalTopicOrder = InOrdinalTopicOrder(trips);

        using var connection = await ConnectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
#if NET
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
#else
        using var transaction = connection.BeginTransaction();
#endif
        using var command = CreateCommand(connection);
        command.Transaction = transaction;
        command.CommandText = UpsertBatch(inOrdinalTopicOrder.Count);
        AddTripParameters(command, inOrdinalTopicOrder);

        var results = new List<TripWriteResult>(inOrdinalTopicOrder.Count);
        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            do
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    results.Add(ToResult(reader));
            } while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));
        }

#if NET
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
#else
        transaction.Commit();
#endif
        return results;
    }

    private string UpsertBatch(int count)
    {
        var batch = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            batch.AppendLine($"""
                UPDATE {QualifiedTableName} WITH (UPDLOCK, SERIALIZABLE)
                SET [PriorTripTime] = CASE WHEN [EndedAt] IS NULL THEN [TripTime] END,
                    [EndedAt] = CASE WHEN [EndedAt] IS NOT NULL AND {StampParameter(i)} <= [TripTime] THEN [EndedAt] END,
                    [TripTime] = CASE WHEN {StampParameter(i)} > [TripTime] THEN {StampParameter(i)} ELSE [TripTime] END
                OUTPUT INSERTED.[Topic], INSERTED.[PriorTripTime], INSERTED.[TripTime], INSERTED.[EndedAt]
                WHERE [Topic] = {TopicParameter(i)};
                IF @@ROWCOUNT = 0
                    INSERT INTO {QualifiedTableName} ([Topic], [TripTime], [PriorTripTime], [EndedAt])
                    OUTPUT INSERTED.[Topic], INSERTED.[PriorTripTime], INSERTED.[TripTime], INSERTED.[EndedAt]
                    VALUES ({TopicParameter(i)}, {StampParameter(i)}, NULL, NULL);
                """);
        }

        return batch.ToString();
    }

    private static string QualifiedTable(string tableName, string? schemaName)
    {
        RequireSafeNames(tableName, schemaName);

        return schemaName is null ? $"[{tableName}]" : $"[{schemaName}].[{tableName}]";
    }
}
