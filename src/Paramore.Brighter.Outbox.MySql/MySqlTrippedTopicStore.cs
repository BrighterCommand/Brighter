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
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Outbox.MySql;

/// <summary>
/// Shares tripped topics between instances through a table in the outbox's MySQL database.
/// </summary>
/// <remarks>
/// The store keeps one row per topic, keeps the later of the stored and written stamps, and marks a row ended rather than
/// deleting it, so the application's database user needs no <c>DELETE</c> permission. Stamps are stored as UTC.
/// </remarks>
public class MySqlTrippedTopicStore : RelationDatabaseTrippedTopicStore
{
    private const string DEFAULT_TABLE_NAME = "BrighterTrippedTopics";
    private const string EXPIRED_ROW_PREDICATE = "`EndedAt` IS NULL AND `TripTime` <= @cutoff";

    /// <summary>
    /// Creates a store over the trip table.
    /// </summary>
    /// <param name="connectionProvider">Provides the connection to the database that holds the trip table.</param>
    /// <param name="tableName">The name of the trip table, at most 43 characters so that the index name <c>idx_{tableName}_EndedAt_TripTime</c> fits MySQL's 64-character limit.</param>
    /// <param name="schemaName">The schema of the trip table, or null for the connection's default schema.</param>
    /// <param name="commandTimeout">
    /// How long a command may run, rounded up to whole seconds with a minimum of one. Null means 2 seconds, the default store timeout.
    /// </param>
    public MySqlTrippedTopicStore(
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
    /// <param name="tableName">The name of the trip table, at most 43 characters so that the index name <c>idx_{tableName}_EndedAt_TripTime</c> fits MySQL's 64-character limit.</param>
    /// <param name="schemaName">The schema (database) of the trip table, or null to use the configuration's schema.</param>
    /// <param name="commandTimeout">
    /// How long a command may run, rounded up to whole seconds with a minimum of one. Null means 2 seconds, the default store timeout.
    /// </param>
    public MySqlTrippedTopicStore(
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
    /// <param name="tableName">The name of the trip table, at most 43 characters so that the index name <c>idx_{tableName}_EndedAt_TripTime</c> fits MySQL's 64-character limit.</param>
    /// <param name="schemaName">The schema of the trip table, or null for the connection's default schema.</param>
    /// <returns>The parameterised DELETE.</returns>
    public static string GetPurgeSql(string tableName, string? schemaName = null) =>
        $"DELETE FROM {QualifiedTable(tableName, schemaName)} WHERE `EndedAt` IS NOT NULL AND `EndedAt` < @EndedBefore;";

    /// <summary>
    /// Gets the SQL that creates the trip table and the secondary index that serves the read's ranged update.
    /// </summary>
    /// <param name="tableName">The name of the trip table, at most 43 characters so that the index name <c>idx_{tableName}_EndedAt_TripTime</c> fits MySQL's 64-character limit.</param>
    /// <param name="schemaName">The schema of the trip table, or null for the connection's default schema.</param>
    /// <returns>The DDL, safe to run more than once.</returns>
    public static string GetDDL(string tableName, string? schemaName = null)
    {
        var qualifiedTable = QualifiedTable(tableName, schemaName);

        return $"""
                CREATE TABLE IF NOT EXISTS {qualifiedTable} (
                    `Topic` VARCHAR(255) NOT NULL,
                    `TripTime` DATETIME(6) NOT NULL,
                    `PriorTripTime` DATETIME(6) NULL,
                    `EndedAt` DATETIME(6) NULL,
                    PRIMARY KEY (`Topic`),
                    INDEX `idx_{tableName}_EndedAt_TripTime` (`EndedAt`, `TripTime`)
                ) ENGINE = InnoDB;
                """;
    }

    /// <inheritdoc />
    protected override bool IsDeadlock(Exception exception) => exception is MySqlException { ErrorCode: MySqlErrorCode.LockDeadlock };

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<TopicTrip>> EndExpiredOnceAsync(
        TripWindow window,
        CancellationToken cancellationToken)
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        var open = new OpenTransaction(connection, transaction);

        var candidates = await QueryTripsAsync(
            connection, transaction,
            $"SELECT `Topic`, `TripTime` FROM {QualifiedTableName} WHERE {EXPIRED_ROW_PREDICATE}",
            [("@cutoff", window.Cutoff)], cancellationToken).ConfigureAwait(false);

        var ended = await LockStillExpiredAsync(open, candidates, window.Cutoff, cancellationToken).ConfigureAwait(false);
        if (ended.Count > 0)
            await MarkEndedAsync(open, ended, window.Now, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ended;
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<TopicTrip>> ReadLiveOnceAsync(
        TripWindow window,
        CancellationToken cancellationToken)
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await QueryTripsAsync(
            connection, null,
            $"SELECT `Topic`, `TripTime` FROM {QualifiedTableName} WHERE `EndedAt` IS NULL AND `TripTime` > @cutoff",
            [("@cutoff", window.Cutoff)], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<TripWriteResult>> WriteOnceAsync(
        IReadOnlyList<TopicTrip> trips,
        CancellationToken cancellationToken)
    {
        var inOrdinalTopicOrder = InOrdinalTopicOrder(trips);

        await using var connection = await ConnectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var open = new OpenTransaction(connection, transaction);

        await using (var upsert = CreateCommand(open))
        {
            upsert.CommandText = UpsertStatement(inOrdinalTopicOrder.Count);
            AddTripParameters(upsert, inOrdinalTopicOrder);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var results = await ReadWrittenRowsAsync(open, inOrdinalTopicOrder, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }

    private async Task<List<TopicTrip>> LockStillExpiredAsync(
        OpenTransaction open,
        IReadOnlyList<TopicTrip> candidates,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var locked = new List<TopicTrip>(candidates.Count);
        if (candidates.Count == 0)
            return locked;

        var inOrdinalTopicOrder = InOrdinalTopicOrder(candidates);
        await using var command = CreateCommand(open);
        command.CommandText =
            $"SELECT `Topic`, `TripTime` FROM {QualifiedTableName} WHERE `Topic` IN ({TopicParameters(inOrdinalTopicOrder.Count)}) AND {EXPIRED_ROW_PREDICATE} FOR UPDATE";
        AddTopicParameters(command, inOrdinalTopicOrder.Select(trip => trip.Topic));
        AddStampParameter(command, "@cutoff", cutoff);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        locked.AddRange(await ReadTripsAsync(reader, cancellationToken).ConfigureAwait(false));
        return locked;
    }

    private async Task MarkEndedAsync(
        OpenTransaction open,
        IReadOnlyList<TopicTrip> locked,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(open);
        command.CommandText =
            $"UPDATE {QualifiedTableName} SET `EndedAt` = @now WHERE `Topic` IN ({TopicParameters(locked.Count)})";
        AddStampParameter(command, "@now", now);
        AddTopicParameters(command, locked.Select(trip => trip.Topic));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<TripWriteResult>> ReadWrittenRowsAsync(
        OpenTransaction open,
        List<TopicTrip> written,
        CancellationToken cancellationToken)
    {
        await using var select = CreateCommand(open);
        select.CommandText =
            $"SELECT `Topic`, `PriorTripTime`, `TripTime`, `EndedAt` FROM {QualifiedTableName} WHERE `Topic` IN ({TopicParameters(written.Count)})";
        AddTopicParameters(select, written.Select(trip => trip.Topic));

        var results = new List<TripWriteResult>(written.Count);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            results.Add(ToResult(reader));
        return results;
    }

    private static string TopicParameters(int count) =>
        string.Join(", ", Enumerable.Range(0, count).Select(TopicParameter));

    private static void AddTopicParameters(DbCommand command, IEnumerable<RoutingKey> topics)
    {
        var index = 0;
        foreach (var topic in topics)
            AddParameter(command, TopicParameter(index++), DbType.String, topic.Value);
    }

    private DbCommand CreateCommand(OpenTransaction open)
    {
        var command = CreateCommand(open.Connection);
        command.Transaction = open.Transaction;
        return command;
    }

    private string UpsertStatement(int count)
    {
        var values = TripValuesList(count);

        return $"""
                INSERT INTO {QualifiedTableName} (`Topic`, `TripTime`)
                VALUES {values}
                ON DUPLICATE KEY UPDATE
                    `PriorTripTime` = IF(`EndedAt` IS NULL, `TripTime`, NULL),
                    `EndedAt` = IF(`EndedAt` IS NOT NULL AND VALUES(`TripTime`) <= `TripTime`, `EndedAt`, NULL),
                    `TripTime` = GREATEST(`TripTime`, VALUES(`TripTime`));
                """;
    }

    /// <inheritdoc />
    protected override void AddStampParameter(DbCommand command, string name, DateTimeOffset stamp) =>
        AddParameter(command, name, DbType.DateTime, stamp.UtcDateTime);

    /// <inheritdoc />
    protected override DateTimeOffset ReadStamp(DbDataReader reader, int ordinal) =>
        new(DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc));

    private static string QualifiedTable(string tableName, string? schemaName)
    {
        RequireSafeNames(tableName, schemaName);

        return schemaName is null ? $"`{tableName}`" : $"`{schemaName}`.`{tableName}`";
    }

    private readonly record struct OpenTransaction(DbConnection Connection, DbTransaction Transaction);
}
