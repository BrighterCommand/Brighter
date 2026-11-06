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

namespace Paramore.Brighter.Outbox.MsSql;

/// <summary>
/// Shares tripped topics between instances through a table in the outbox's SQL Server database.
/// </summary>
/// <remarks>
/// The store keeps one row per topic, keeps the later of the stored and written stamps, and marks a row ended rather than
/// deleting it, so the application's database user needs no <c>DELETE</c> permission.
/// </remarks>
public class MsSqlTrippedTopicStore : IAmATrippedTopicStore
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
    public MsSqlTrippedTopicStore(
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
    public async Task<TrippedTopicStoreRead> ReadAsync(
        DateTimeOffset now,
        TimeSpan tripLength,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT [Topic], [TripTime] FROM {_qualifiedTable} WHERE [EndedAt] IS NULL";

        var live = new List<TopicTrip>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            live.Add(new TopicTrip(new RoutingKey(reader.GetString(0)), reader.GetFieldValue<DateTimeOffset>(1)));

        return new TrippedTopicStoreRead(live, []);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TripWriteResult>> WriteAsync(
        IReadOnlyList<TopicTrip> trips,
        CancellationToken cancellationToken = default)
    {
        var inOrdinalTopicOrder = InOrdinalTopicOrder(trips);

        using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpsertBatch(inOrdinalTopicOrder.Count);
        for (var i = 0; i < inOrdinalTopicOrder.Count; i++)
        {
            AddParameter(command, $"@topic{i}", DbType.String, inOrdinalTopicOrder[i].Topic.Value);
            AddParameter(command, $"@t{i}", DbType.DateTimeOffset, inOrdinalTopicOrder[i].TripTime);
        }

        var results = new List<TripWriteResult>(inOrdinalTopicOrder.Count);
        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            do
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    results.Add(ToResult(reader));
            } while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));
        }

        transaction.Commit();
        return results;
    }

    private string UpsertBatch(int count)
    {
        var batch = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            batch.AppendLine($"""
                UPDATE {_qualifiedTable} WITH (UPDLOCK, SERIALIZABLE)
                SET [PriorTripTime] = CASE WHEN [EndedAt] IS NULL THEN [TripTime] END,
                    [EndedAt] = CASE WHEN [EndedAt] IS NOT NULL AND @t{i} <= [TripTime] THEN [EndedAt] END,
                    [TripTime] = CASE WHEN @t{i} > [TripTime] THEN @t{i} ELSE [TripTime] END
                OUTPUT INSERTED.[Topic], INSERTED.[PriorTripTime], INSERTED.[TripTime], INSERTED.[EndedAt]
                WHERE [Topic] = @topic{i};
                IF @@ROWCOUNT = 0
                    INSERT INTO {_qualifiedTable} ([Topic], [TripTime], [PriorTripTime], [EndedAt])
                    OUTPUT INSERTED.[Topic], INSERTED.[PriorTripTime], INSERTED.[TripTime], INSERTED.[EndedAt]
                    VALUES (@topic{i}, @t{i}, NULL, NULL);
                """);
        }

        return batch.ToString();
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

        return schemaName is null ? $"[{tableName}]" : $"[{schemaName}].[{tableName}]";
    }
}
