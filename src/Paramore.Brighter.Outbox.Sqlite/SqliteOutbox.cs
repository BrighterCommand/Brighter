#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

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
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Sqlite;

namespace Paramore.Brighter.Outbox.Sqlite;

/// <summary>
/// Implements an outbox using Sqlite as a backing store
/// </summary>
public class SqliteOutbox : RelationDatabaseOutbox
{
    private const int SqliteDuplicateKeyError = 1555;
    private const int SqliteUniqueKeyError = 19;
    private readonly object _abandonedReadsLock = new();
    private Task _abandonedReads = Task.CompletedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteOutbox" /> class.
    /// </summary>
    /// <param name="configuration">The configuration to connect to this data store</param>
    /// <param name="connectionProvider">Provides a connection to the Db that allows us to enlist in an ambient transaction</param>
    public SqliteOutbox(
        IAmARelationalDatabaseConfiguration configuration,
        IAmARelationalDbConnectionProvider connectionProvider
    )
        : base(DbSystem.Sqlite, configuration, connectionProvider, 
            new SqliteQueries(), ApplicationLogging.CreateLogger<SqliteOutbox>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteOutbox" /> class.
    /// </summary>
    /// <param name="configuration">The configuration to connect to this data store</param>
    public SqliteOutbox(IAmARelationalDatabaseConfiguration configuration)
        : this(configuration, new SqliteConnectionProvider(configuration))
    {
    }

    /// <inheritdoc />
    protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex)
    {
        return ex is SqliteException { SqliteErrorCode: SqliteDuplicateKeyError or SqliteUniqueKeyError };
    }

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
    {
        return new SqliteParameter(parameterName, value ?? DBNull.Value);
    }

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, DbType dbType, object? value)
    {
        return new SqliteParameter(parameterName, value ?? DBNull.Value) { DbType = dbType };
    }

    /// <inheritdoc />
    protected override DateTimeOffset GetTimeStamp(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, TimestampColumnName, out var ordinal) || dr.IsDBNull(ordinal))
        {
            return DateTimeOffset.MinValue;
        }

        var reader = (SqliteDataReader)dr;
        var dataTime = reader.GetDateTimeOffset(ordinal);
        return dataTime; 
    }

    /// <inheritdoc />
    /// <remarks>
    /// Microsoft.Data.Sqlite runs a step that is blocked on a lock synchronously, so the token cannot interrupt it.
    /// The read runs on its own thread and the caller stops waiting for it once the token is cancelled. That
    /// thread stays busy until the read ends. A read blocked on a lock with no command timeout ends only when
    /// the lock is released.
    /// While a read whose caller stopped waiting has not ended, a later read with a token that can be cancelled
    /// waits for it before it starts, and ends with <see cref="OperationCanceledException"/> without reading if its
    /// own token is cancelled first.
    /// </remarks>
    protected override async Task<T> ReadFromStoreAsync<T>(
        Func<DbConnection, DbCommand> commandFunc,
        Func<DbDataReader, Task<T>> resultFunc,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return await base.ReadFromStoreAsync(commandFunc, resultFunc, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        if (!await EndsBeforeCancellationAsync(AbandonedReads, cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
        {
            throw new OperationCanceledException(cancellationToken);
        }

        var read = Task.Run(() => base.ReadFromStoreAsync(commandFunc, resultFunc, cancellationToken), CancellationToken.None);
        if (await EndsBeforeCancellationAsync(read, cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
        {
            return await read.ConfigureAwait(ContinueOnCapturedContext);
        }

        Abandon(read);
        throw new OperationCanceledException(cancellationToken);
    }

    private Task AbandonedReads
    {
        get
        {
            lock (_abandonedReadsLock)
            {
                return _abandonedReads;
            }
        }
    }

    private void Abandon(Task read)
    {
        var observed = read.ContinueWith(abandoned => abandoned.Exception, TaskScheduler.Default);
        lock (_abandonedReadsLock)
        {
            _abandonedReads = _abandonedReads.IsCompleted ? observed : Task.WhenAll(_abandonedReads, observed);
        }
    }

    private async Task<bool> EndsBeforeCancellationAsync(Task task, CancellationToken cancellationToken)
    {
        if (task.IsCompleted)
        {
            return true;
        }

        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
        {
            return await Task.WhenAny(task, cancelled.Task).ConfigureAwait(ContinueOnCapturedContext) == task;
        }
    }
}
