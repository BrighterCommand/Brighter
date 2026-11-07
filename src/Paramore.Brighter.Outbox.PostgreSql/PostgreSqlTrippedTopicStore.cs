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
public class PostgreSqlTrippedTopicStore : IAmATrippedTopicStore
{
    private const string DEFAULT_TABLE_NAME = "BrighterTrippedTopics";

    private readonly IAmARelationalDbConnectionProvider _connectionProvider;
    private readonly string _qualifiedTable;

    /// <summary>
    /// Creates a store over the trip table.
    /// </summary>
    /// <param name="connectionProvider">Provides the connection to the database that holds the trip table.</param>
    /// <param name="tableName">The name of the trip table.</param>
    /// <param name="schemaName">The schema of the trip table, or null for the connection's default schema.</param>
    public PostgreSqlTrippedTopicStore(
        IAmARelationalDbConnectionProvider connectionProvider,
        string tableName = DEFAULT_TABLE_NAME,
        string? schemaName = null)
    {
        _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
        _qualifiedTable = QualifiedTable(tableName, schemaName);
    }

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
    public async Task<TrippedTopicStoreRead> ReadAsync(
        DateTimeOffset now,
        TimeSpan tripLength,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {_qualifiedTable} SET ended_at = @now
            WHERE ended_at IS NULL AND trip_time <= @cutoff
            RETURNING topic, trip_time;
            SELECT topic, trip_time FROM {_qualifiedTable} WHERE ended_at IS NULL;
            """;
        AddParameter(command, "@now", DbType.DateTimeOffset, now.ToUniversalTime());
        AddParameter(command, "@cutoff", DbType.DateTimeOffset, (now - tripLength).ToUniversalTime());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var ended = await ReadTripsAsync(reader, cancellationToken).ConfigureAwait(false);
        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
        var live = await ReadTripsAsync(reader, cancellationToken).ConfigureAwait(false);

        return new TrippedTopicStoreRead(live, ended);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TripWriteResult>> WriteAsync(
        IReadOnlyList<TopicTrip> trips,
        CancellationToken cancellationToken = default)
    {
        var inOrdinalTopicOrder = InOrdinalTopicOrder(trips);

        await using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = UpsertStatement(inOrdinalTopicOrder.Count);
        for (var i = 0; i < inOrdinalTopicOrder.Count; i++)
        {
            AddParameter(command, $"@topic{i}", DbType.String, inOrdinalTopicOrder[i].Topic.Value);
            AddParameter(command, $"@t{i}", DbType.DateTimeOffset, inOrdinalTopicOrder[i].TripTime.ToUniversalTime());
        }

        var results = new List<TripWriteResult>(inOrdinalTopicOrder.Count);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            results.Add(ToResult(reader));

        return results;
    }

    private string UpsertStatement(int count)
    {
        var values = string.Join(", ", Enumerable.Range(0, count).Select(i => $"(@topic{i}, @t{i})"));

        return $"""
                INSERT INTO {_qualifiedTable} AS t (topic, trip_time)
                VALUES {values}
                ON CONFLICT (topic) DO UPDATE SET
                    prior_trip_time = CASE WHEN t.ended_at IS NULL THEN t.trip_time END,
                    ended_at = CASE WHEN t.ended_at IS NOT NULL AND EXCLUDED.trip_time <= t.trip_time THEN t.ended_at END,
                    trip_time = GREATEST(t.trip_time, EXCLUDED.trip_time)
                RETURNING t.topic, t.prior_trip_time, t.trip_time, t.ended_at;
                """;
    }

    private static List<TopicTrip> InOrdinalTopicOrder(IReadOnlyList<TopicTrip> trips)
    {
        var distinctTopics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trip in trips)
        {
            if (!distinctTopics.Add(trip.Topic.Value))
                throw new ArgumentException($"The topic {trip.Topic.Value} appears more than once in the write", nameof(trips));
        }

        return trips.OrderBy(trip => trip.Topic.Value, StringComparer.Ordinal).ToList();
    }

    private static async Task<List<TopicTrip>> ReadTripsAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var trips = new List<TopicTrip>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            trips.Add(new TopicTrip(new RoutingKey(reader.GetString(0)), reader.GetFieldValue<DateTimeOffset>(1)));
        return trips;
    }

    private static TripWriteResult ToResult(DbDataReader reader) =>
        new(
            new RoutingKey(reader.GetString(0)),
            reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            !reader.IsDBNull(3));

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string QualifiedTable(string tableName, string? schemaName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("You must provide a table name for the trip table", nameof(tableName));

        return PgIdentifier.QuoteQualified(schemaName, tableName);
    }
}
