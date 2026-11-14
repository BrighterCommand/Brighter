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
    /// <remarks>
    /// Runs <see cref="EndExpiredOnceAsync"/>, and runs it once more if the database chose it as a deadlock victim.
    /// </remarks>
    public Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(
        TripWindow window,
        CancellationToken cancellationToken = default) =>
        RunWithDeadlockRetryAsync(() => EndExpiredOnceAsync(window, cancellationToken));

    /// <inheritdoc />
    /// <remarks>
    /// Runs <see cref="ReadLiveOnceAsync"/>, and runs it once more if the database chose it as a deadlock victim.
    /// </remarks>
    public Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(
        TripWindow window,
        CancellationToken cancellationToken = default) =>
        RunWithDeadlockRetryAsync(() => ReadLiveOnceAsync(window, cancellationToken));

    /// <inheritdoc />
    /// <remarks>
    /// Runs <see cref="WriteOnceAsync"/>, and runs it once more if the database chose it as a deadlock victim.
    /// </remarks>
    public Task<IReadOnlyList<TripWriteResult>> WriteAsync(
        IReadOnlyList<TopicTrip> trips,
        CancellationToken cancellationToken = default) =>
        RunWithDeadlockRetryAsync(() => WriteOnceAsync(trips, cancellationToken));

    /// <summary>
    /// Marks the trips that have run out as ended and returns them, once, in the store's dialect. The base retries it after a deadlock.
    /// </summary>
    /// <param name="window">The time of the call and how long a trip lasts.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The trips this call marked ended.</returns>
    protected abstract Task<IReadOnlyList<TopicTrip>> EndExpiredOnceAsync(
        TripWindow window,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the live trips once, in the store's dialect, changing nothing. The base retries it after a deadlock.
    /// </summary>
    /// <param name="window">The time of the read and how long a trip lasts.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The trips that are not marked ended and have not run out.</returns>
    protected abstract Task<IReadOnlyList<TopicTrip>> ReadLiveOnceAsync(
        TripWindow window,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes the trips once, in the store's dialect. The base retries it after a deadlock.
    /// </summary>
    /// <param name="trips">The trips to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What the store holds for each trip after the write.</returns>
    protected abstract Task<IReadOnlyList<TripWriteResult>> WriteOnceAsync(
        IReadOnlyList<TopicTrip> trips,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs an operation and, when it fails with a deadlock, runs it once more.
    /// </summary>
    /// <remarks>
    /// The operation runs at most twice. A second failure, of any kind, reaches the caller.
    /// </remarks>
    /// <typeparam name="T">The type the operation returns.</typeparam>
    /// <param name="operation">The operation, which must be safe to run again.</param>
    /// <returns>What the operation returns.</returns>
    private async Task<T> RunWithDeadlockRetryAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            return await operation().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Says whether a failure is the database choosing the operation as a deadlock victim.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>True when the operation should be run again. The base returns false.</returns>
    protected virtual bool IsDeadlock(Exception exception) => false;

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
    /// Adds the topic and stamp parameters of each trip, named by <see cref="TopicParameter"/> and <see cref="StampParameter"/>.
    /// </summary>
    /// <param name="command">The command to add the parameters to.</param>
    /// <param name="trips">The trips, in the order the command's SQL numbers them.</param>
    protected void AddTripParameters(DbCommand command, IReadOnlyList<TopicTrip> trips)
    {
        for (var i = 0; i < trips.Count; i++)
        {
            AddParameter(command, TopicParameter(i), DbType.String, trips[i].Topic.Value);
            AddStampParameter(command, StampParameter(i), trips[i].TripTime);
        }
    }

    /// <summary>
    /// Adds a parameter holding a stamp, in the form the dialect stores it.
    /// </summary>
    /// <remarks>
    /// The base adds the stamp as a <see cref="DateTimeOffset"/>. A derived store overrides this when its database needs another form, such as UTC.
    /// </remarks>
    /// <param name="command">The command to add the parameter to.</param>
    /// <param name="name">The parameter's name.</param>
    /// <param name="stamp">The stamp.</param>
    protected virtual void AddStampParameter(DbCommand command, string name, DateTimeOffset stamp) =>
        AddParameter(command, name, DbType.DateTimeOffset, stamp);

    /// <summary>
    /// Gets the name of the parameter holding the topic of the trip at an index.
    /// </summary>
    /// <param name="index">The trip's position in the write.</param>
    /// <returns>The parameter name.</returns>
    protected static string TopicParameter(int index) => $"@topic{index}";

    /// <summary>
    /// Gets the name of the parameter holding the stamp of the trip at an index.
    /// </summary>
    /// <param name="index">The trip's position in the write.</param>
    /// <returns>The parameter name.</returns>
    protected static string StampParameter(int index) => $"@t{index}";

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
    /// Runs a query that selects topic and trip time and reads its rows into trips.
    /// </summary>
    /// <param name="connection">The connection to run the query on.</param>
    /// <param name="transaction">The transaction to run it in, or null for none.</param>
    /// <param name="sql">The query.</param>
    /// <param name="stamps">The stamp parameters the query uses, added in the dialect's form by <see cref="AddStampParameter"/>.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The trips.</returns>
    protected async Task<List<TopicTrip>> QueryTripsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        IReadOnlyList<(string Name, DateTimeOffset Stamp)> stamps,
        CancellationToken cancellationToken)
    {
        using var command = CreateCommand(connection);
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, stamp) in stamps)
            AddStampParameter(command, name, stamp);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await ReadTripsAsync(reader, cancellationToken).ConfigureAwait(false);
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
