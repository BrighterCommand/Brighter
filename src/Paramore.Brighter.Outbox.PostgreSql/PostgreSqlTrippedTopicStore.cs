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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Outbox.PostgreSql;

/// <summary>
/// Shares tripped topics between instances through a table in the outbox's PostgreSQL database.
/// </summary>
/// <remarks>
/// The store keeps one row per topic, keeps the later of the stored and written stamps, and marks a row ended rather than
/// deleting it, so the application's database user needs no <c>DELETE</c> permission.
/// </remarks>
public class PostgreSqlTrippedTopicStore : RelationDatabaseTrippedTopicStore
{
    private const string DEFAULT_TABLE_NAME = "BrighterTrippedTopics";

    /// <summary>
    /// Creates a store over the trip table.
    /// </summary>
    /// <param name="connectionProvider">Provides the connection to the database that holds the trip table.</param>
    /// <param name="tableName">The name of the trip table.</param>
    /// <param name="schemaName">The schema of the trip table, or null for the connection's default schema.</param>
    /// <param name="commandTimeout">
    /// How long a command may run, rounded up to whole seconds with a minimum of one. Null means 2 seconds, the default store timeout.
    /// </param>
    public PostgreSqlTrippedTopicStore(
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
    /// <param name="tableName">The name of the trip table.</param>
    /// <param name="schemaName">The schema of the trip table, or null to use the configuration's schema.</param>
    /// <param name="commandTimeout">
    /// How long a command may run, rounded up to whole seconds with a minimum of one. Null means 2 seconds, the default store timeout.
    /// </param>
    public PostgreSqlTrippedTopicStore(
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
    /// <param name="tableName">The name of the trip table.</param>
    /// <param name="schemaName">The schema of the trip table, or null for the connection's default schema.</param>
    /// <returns>The parameterised DELETE.</returns>
    public static string GetPurgeSql(string tableName, string? schemaName = null) =>
        $"DELETE FROM {QualifiedTable(tableName, schemaName)} WHERE ended_at IS NOT NULL AND ended_at < @EndedBefore;";

    /// <summary>
    /// Gets the SQL that creates the trip table and the secondary index that serves the read's ranged update.
    /// </summary>
    /// <param name="tableName">The name of the trip table.</param>
    /// <param name="schemaName">The schema of the trip table, or null for the connection's default schema.</param>
    /// <returns>The DDL, safe to run more than once.</returns>
    public static string GetDDL(string tableName, string? schemaName = null)
    {
        var qualifiedTable = QualifiedTable(tableName, schemaName);

        return $"""
                CREATE TABLE IF NOT EXISTS {qualifiedTable} (
                    topic character varying(255) NOT NULL PRIMARY KEY,
                    trip_time timestamptz NOT NULL,
                    prior_trip_time timestamptz NULL,
                    ended_at timestamptz NULL
                );
                CREATE INDEX IF NOT EXISTS idx_{PgIdentifier.Normalize(tableName)}_ended_at_trip_time ON {qualifiedTable} (ended_at, trip_time);
                """;
    }

    /// <inheritdoc />
    protected override bool IsDeadlock(Exception exception) => exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected };

    /// <inheritdoc />
    protected override void AddStampParameter(DbCommand command, string name, DateTimeOffset stamp) =>
        AddParameter(command, name, DbType.DateTimeOffset, stamp.ToUniversalTime());

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<TopicTrip>> EndExpiredOnceAsync(
        TripWindow window,
        CancellationToken cancellationToken)
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await QueryTripsAsync(
            connection, null,
            $"""
            UPDATE {QualifiedTableName} SET ended_at = @now
            WHERE ended_at IS NULL AND trip_time <= @cutoff
            RETURNING topic, trip_time;
            """,
            [("@now", window.Now), ("@cutoff", window.Cutoff)], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<TopicTrip>> ReadLiveOnceAsync(
        TripWindow window,
        CancellationToken cancellationToken)
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await QueryTripsAsync(
            connection, null,
            $"SELECT topic, trip_time FROM {QualifiedTableName} WHERE ended_at IS NULL AND trip_time > @cutoff;",
            [("@cutoff", window.Cutoff)], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<TripWriteResult>> WriteOnceAsync(
        IReadOnlyList<TopicTrip> trips,
        CancellationToken cancellationToken)
    {
        var inOrdinalTopicOrder = InOrdinalTopicOrder(trips);

        await using var connection = await ConnectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(connection);
        command.CommandText = UpsertStatement(inOrdinalTopicOrder.Count);
        AddTripParameters(command, inOrdinalTopicOrder);

        var results = new List<TripWriteResult>(inOrdinalTopicOrder.Count);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            results.Add(ToResult(reader));

        return results;
    }

    private string UpsertStatement(int count)
    {
        var values = TripValuesList(count);

        return $"""
                INSERT INTO {QualifiedTableName} AS t (topic, trip_time)
                VALUES {values}
                ON CONFLICT (topic) DO UPDATE SET
                    prior_trip_time = CASE WHEN t.ended_at IS NULL THEN t.trip_time END,
                    ended_at = CASE WHEN t.ended_at IS NOT NULL AND EXCLUDED.trip_time <= t.trip_time THEN t.ended_at END,
                    trip_time = GREATEST(t.trip_time, EXCLUDED.trip_time)
                RETURNING t.topic, t.prior_trip_time, t.trip_time, t.ended_at;
                """;
    }

    private static string QualifiedTable(string tableName, string? schemaName)
    {
        RequireSafeNames(tableName, schemaName);

        return PgIdentifier.QuoteQualified(schemaName, tableName);
    }
}
