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

namespace Paramore.Brighter.CircuitBreaker;

/// <summary>
/// The base of the trip stores that keep tripped topics in a table in a relational database.
/// </summary>
/// <remarks>
/// The base holds what the stores share: the rule that a write names each topic once and applies topics in ordinal order,
/// the command timeout, command and parameter creation, and the mapping of rows to trips. A derived store keeps its own
/// dialect: the SQL, the quoting, the shape of its write and read, and its connection and transaction handling.
/// </remarks>
public abstract class RelationDatabaseTrippedTopicStore : IAmATrippedTopicStore
{
    private static readonly TimeSpan s_defaultCommandTimeout = TimeSpan.FromSeconds(2);

    private readonly int _commandTimeoutSeconds;

    /// <summary>
    /// Creates a store over the trip table.
    /// </summary>
    /// <param name="connectionProvider">Provides the connection to the database that holds the trip table.</param>
    /// <param name="qualifiedTableName">The trip table's name, quoted and qualified by its schema for the dialect.</param>
    /// <param name="commandTimeout">
    /// How long a command may run, rounded up to whole seconds with a minimum of one. Null means 2 seconds, the default store timeout.
    /// </param>
    protected RelationDatabaseTrippedTopicStore(
        IAmARelationalDbConnectionProvider connectionProvider,
        string qualifiedTableName,
        TimeSpan? commandTimeout)
    {
        ConnectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
        QualifiedTableName = qualifiedTableName;
        _commandTimeoutSeconds = WholeSecondsAtLeastOne(commandTimeout ?? s_defaultCommandTimeout);
    }

    /// <summary>
    /// Provides the connection to the database that holds the trip table.
    /// </summary>
    protected IAmARelationalDbConnectionProvider ConnectionProvider { get; }

    /// <summary>
    /// The trip table's name, quoted and qualified by its schema for the dialect.
    /// </summary>
    protected string QualifiedTableName { get; }

    /// <inheritdoc />
    public abstract Task<TrippedTopicStoreRead> ReadAsync(
        DateTimeOffset now,
        TimeSpan tripLength,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<TripWriteResult>> WriteAsync(
        IReadOnlyList<TopicTrip> trips,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a command on the connection with the store's command timeout.
    /// </summary>
    /// <param name="connection">The connection to create the command on.</param>
    /// <returns>The command.</returns>
    protected DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandTimeout = _commandTimeoutSeconds;
        return command;
    }

    /// <summary>
    /// Adds a parameter to a command.
    /// </summary>
    /// <param name="command">The command to add the parameter to.</param>
    /// <param name="name">The parameter's name.</param>
    /// <param name="type">The parameter's type.</param>
    /// <param name="value">The parameter's value.</param>
    protected static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// Checks that a write names each topic once, and orders its trips by topic, ordinally.
    /// </summary>
    /// <remarks>
    /// Every write applies its topics in this order, so that overlapping writes take their row locks in the same order and cannot deadlock.
    /// </remarks>
    /// <param name="trips">The trips to write.</param>
    /// <returns>The trips in ordinal topic order.</returns>
    /// <exception cref="ArgumentException">A topic appears more than once.</exception>
    protected static List<TopicTrip> InOrdinalTopicOrder(IReadOnlyList<TopicTrip> trips)
    {
        var distinctTopics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trip in trips)
        {
            if (!distinctTopics.Add(trip.Topic.Value))
                throw new ArgumentException($"The topic {trip.Topic.Value} appears more than once in the write", nameof(trips));
        }

        return trips.OrderBy(trip => trip.Topic.Value, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Reads the rest of a result set of topic and trip time into trips.
    /// </summary>
    /// <param name="reader">The reader, positioned before the first row.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The trips.</returns>
    protected async Task<List<TopicTrip>> ReadTripsAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var trips = new List<TopicTrip>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            trips.Add(new TopicTrip(new RoutingKey(reader.GetString(0)), ReadStamp(reader, 1)));
        return trips;
    }

    /// <summary>
    /// Maps the current row of topic, prior trip time, trip time and ended marker to a write result.
    /// </summary>
    /// <param name="reader">The reader, positioned on the row.</param>
    /// <returns>The result.</returns>
    protected TripWriteResult ToResult(DbDataReader reader) =>
        new(
            new RoutingKey(reader.GetString(0)),
            reader.IsDBNull(1) ? null : ReadStamp(reader, 1),
            ReadStamp(reader, 2),
            !reader.IsDBNull(3));

    /// <summary>
    /// Reads a stored stamp as a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="reader">The reader, positioned on the row.</param>
    /// <param name="ordinal">The column holding the stamp.</param>
    /// <returns>The stamp.</returns>
    protected virtual DateTimeOffset ReadStamp(DbDataReader reader, int ordinal) =>
        reader.GetFieldValue<DateTimeOffset>(ordinal);

    private static int WholeSecondsAtLeastOne(TimeSpan timeout) => Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));
}
