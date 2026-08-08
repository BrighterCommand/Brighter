#region Licence

/* The MIT License (MIT)
Copyright © 2025 Dominic Hickie <dominichickie@gmail.com>

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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    public abstract class RelationalDatabaseInbox(
        DbSystem dbSystem,
        IAmARelationalDatabaseConfiguration configuration,
        IAmARelationalDbConnectionProvider connectionProvider,
        IRelationalDatabaseInboxQueries queries,
        ILogger logger,
        InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        : IAmAnInboxSync, IAmAnInboxAsync, IAmACausationTrackingInbox
    {
        protected IAmARelationalDatabaseConfiguration DatabaseConfiguration { get; } = configuration;

        protected IAmARelationalDbConnectionProvider ConnectionProvider { get; } = connectionProvider;

        // Non-null only when the backend's query set supports causation tracking; gates the IAmACausationTrackingInbox behaviour.
        private IRelationalDatabaseInboxCausationQueries? CausationQueries => queries as IRelationalDatabaseInboxCausationQueries;

        // Lazily-probed, memoized result of whether the live table actually has the CausationId column.
        // The driver capability (CausationQueries) is static and always present once a backend implements
        // the interface, so it must NOT gate the write — an un-migrated table lacks the column and a
        // causation-aware INSERT would fail (AC10). Populated by the first probe on either the sync or
        // async path and read by both; concurrent first probes are harmless (idempotent, same result).
        // Access is deliberately not synchronised: a stale-null read on a weak memory model just triggers
        // one extra idempotent probe, and both probes resolve to the same value, so the cached answer never
        // changes once written. (`volatile` is not applicable to a nullable value type, hence this note.)
        // Never invalidated: a store constructed before provisioning caches "absent", so a mid-process
        // migration requires a restart.
        private bool? _causationColumnExists;

        /// <inheritdoc/>
        public bool ContinueOnCapturedContext { get; set; }

        /// <inheritdoc/>
        public IAmABrighterTracer? Tracer { private get; set; }

        /// <inheritdoc/>
        public void Add<T>(T command, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds)
            where T : class, IRequest
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.command.id", command.Id.Value },
                { "db.operation.name", ExtractSqlOperationName(queries.AddCommand) },
                { "db.query.text", queries.AddCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Add,
                    DatabaseConfiguration.InBoxTableName, dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var parameters = CreateAddParameters(command, contextKey);
                if (CausationColumnExists())
                {
                    parameters = [.. parameters, CreateSqlParameter("@CausationId", ReadCausationId(requestContext))];
                }
                WriteToStore(
                    connection => CreateAddCommand(connection, timeoutInMilliseconds, parameters),
                    () =>
                    {
                        logger.LogWarning(
                            "Inbox: A duplicate command with the ID {Id} was inserted into the Inbox, ignoring and continuing",
                            command.Id);
                    });
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc/>
        public T Get<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds)
            where T : class, IRequest
        {
            var dbAttributes = new Dictionary<string, string>
            {
                { "db.operation.parameter.command.id", id },
                { "db.operation.name", ExtractSqlOperationName(queries.GetCommand) },
                { "db.query.text", queries.GetCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Get,
                    DatabaseConfiguration.InBoxTableName, dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var parameters = CreateGetParameters(id, contextKey);
                return ReadFromStore(
                    connection => CreateGetCommand(connection, timeoutInMilliseconds, parameters),
                    MapFunction<T>,
                    id);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc/>
        public bool Exists<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds)
            where T : class, IRequest
        {
            var dbAttributes = new Dictionary<string, string>
            {
                { "db.operation.parameter.command.id", id },
                { "db.operation.name", ExtractSqlOperationName(queries.ExistsCommand) },
                { "db.query.text", queries.ExistsCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Exists,
                    DatabaseConfiguration.InBoxTableName, dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var parameters = CreateExistsParameters(id, contextKey);
                return ReadFromStore(
                    connection => CreateExistsCommand(connection, timeoutInMilliseconds, parameters),
                    MapBoolFunction,
                    id);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc/>
        public async Task AddAsync<T>(T command, string contextKey, RequestContext? requestContext,
            int timeoutInMilliseconds, CancellationToken cancellationToken)
            where T : class, IRequest
        {
            var dbAttributes = new Dictionary<string, string>
            {
                { "db.operation.parameter.command.id", command.Id.Value },
                { "db.operation.name", ExtractSqlOperationName(queries.AddCommand) },
                { "db.query.text", queries.AddCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Add,
                    DatabaseConfiguration.InBoxTableName, dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var parameters = CreateAddParameters(command, contextKey);
                if (await CausationColumnExistsAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                {
                    parameters = [.. parameters, CreateSqlParameter("@CausationId", ReadCausationId(requestContext))];
                }
                await WriteToStoreAsync(
                    connection => CreateAddCommand(connection, timeoutInMilliseconds, parameters),
                    () =>
                    {
                        logger.LogWarning(
                            "Inbox: A duplicate command with the ID {Id} was inserted into the Inbox, ignoring and continuing",
                            command.Id);
                    },
                    cancellationToken);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc/>
        public async Task<T> GetAsync<T>(string id, string contextKey, RequestContext? requestContext,
            int timeoutInMilliseconds, CancellationToken cancellationToken)
            where T : class, IRequest
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.command.id", id },
                { "db.operation.name", ExtractSqlOperationName(queries.GetCommand) },
                { "db.query.text", queries.GetCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Get,
                    DatabaseConfiguration.InBoxTableName, dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var parameters = CreateGetParameters(id, contextKey);
                return await ReadFromStoreAsync(
                    connection => CreateGetCommand(connection, timeoutInMilliseconds, parameters),
                    MapFunctionAsync<T>,
                    id,
                    cancellationToken);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsAsync<T>(string id, string contextKey, RequestContext? requestContext,
            int timeoutInMilliseconds, CancellationToken cancellationToken)
            where T : class, IRequest
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.command.id", id },
                { "db.operation.name", ExtractSqlOperationName(queries.ExistsCommand) },
                { "db.query.text", queries.ExistsCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Exists,
                    DatabaseConfiguration.InBoxTableName, dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var parameters = CreateExistsParameters(id, contextKey);
                return await ReadFromStoreAsync(
                    connection => CreateExistsCommand(connection, timeoutInMilliseconds, parameters),
                    MapBoolFunctionAsync,
                    id,
                    cancellationToken);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc />
        public bool SupportsCausationTracking() => CausationColumnExists();

        /// <inheritdoc />
        public Task<bool> SupportsCausationTrackingAsync(CancellationToken cancellationToken = default)
            => CausationColumnExistsAsync(cancellationToken);

        // Memoized live-schema probe shared by SupportsCausationTracking[Async] and the Add gate.
        private bool CausationColumnExists()
        {
            if (CausationQueries is null)
            {
                return false;
            }

            return _causationColumnExists ??= ReadFromStore(
                connection => CreateCommand(connection, GenerateSqlText(CausationQueries.CausationColumnExistsCommand), -1),
                MapBoolFunction,
                string.Empty);
        }

        private async Task<bool> CausationColumnExistsAsync(CancellationToken cancellationToken)
        {
            if (CausationQueries is null)
            {
                return false;
            }

            if (_causationColumnExists is { } cached)
            {
                return cached;
            }

            var exists = await ReadFromStoreAsync(
                connection => CreateCommand(connection, GenerateSqlText(CausationQueries.CausationColumnExistsCommand), -1),
                MapBoolFunctionAsync,
                string.Empty,
                cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            _causationColumnExists = exists;
            return exists;
        }

        /// <inheritdoc />
        public string? GetCausationId(string id, string contextKey, RequestContext? requestContext,
            int timeoutInMilliseconds = -1)
        {
            // Gate the read on the same live-schema probe as the write path: a backend may implement the
            // causation query interface while its table has not been migrated (the CausationId column is
            // absent). Selecting the missing column would throw; degrade to null instead (AC10).
            if (!CausationColumnExists())
            {
                return null;
            }

            var parameters = CreateGetParameters(id, contextKey);
            return ReadFromStore(
                connection => CreateCommand(connection, GenerateSqlText(CausationQueries!.GetCausationIdCommand),
                    timeoutInMilliseconds, parameters),
                MapCausationId,
                id);
        }

        /// <inheritdoc />
        public async Task<string?> GetCausationIdAsync(string id, string contextKey, RequestContext? requestContext,
            int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default)
        {
            // Gate the read on the same live-schema probe as the write path: a backend may implement the
            // causation query interface while its table has not been migrated (the CausationId column is
            // absent). Selecting the missing column would throw; degrade to null instead (AC10).
            if (!await CausationColumnExistsAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
            {
                return null;
            }

            var parameters = CreateGetParameters(id, contextKey);
            return await ReadFromStoreAsync(
                connection => CreateCommand(connection, GenerateSqlText(CausationQueries!.GetCausationIdCommand),
                    timeoutInMilliseconds, parameters),
                MapCausationIdAsync,
                id,
                cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }

        protected abstract bool IsExceptionUniqueOrDuplicateIssue(Exception ex);

        protected virtual void WriteToStore(Func<DbConnection, DbCommand> commandFunc, Action? loggingAction)
        {
            var connection = GetOpenConnection(ConnectionProvider);

            using var command = commandFunc.Invoke(connection);
            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception exception)
            {
                if (!IsExceptionUniqueOrDuplicateIssue(exception))
                {
                    throw;
                }

                loggingAction?.Invoke();
            }
            finally
            {
                FinishWrite(connection);
            }
        }

        protected virtual async Task WriteToStoreAsync(
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction,
            CancellationToken cancellationToken
        )
        {
            var connection = await GetOpenConnectionAsync(ConnectionProvider, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

#if NETSTANDARD
            using var command = commandFunc.Invoke(connection);
#else
            await using var command = commandFunc.Invoke(connection);
#endif

            try
            {
                await command
                    .ExecuteNonQueryAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
            }
            catch (Exception exception)
            {
                if (!IsExceptionUniqueOrDuplicateIssue(exception))
                {
                    throw;
                }

                loggingAction?.Invoke();
            }
            finally
            {
                FinishWrite(connection);
            }
        }

        protected virtual T ReadFromStore<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, string, T> resultFunc,
            string commandId
        )
        {
            var connection = GetOpenConnection(ConnectionProvider);
            using var command = commandFunc.Invoke(connection);
            try
            {
                return resultFunc.Invoke(command.ExecuteReader(), commandId);
            }
            finally
            {
                connection.Close();
            }
        }

        protected virtual async Task<T> ReadFromStoreAsync<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, string, CancellationToken, Task<T>> resultFunc,
            string commandId,
            CancellationToken cancellationToken
        )
        {
            var connection = await GetOpenConnectionAsync(ConnectionProvider, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

#if NETSTANDARD
            using var command = commandFunc.Invoke(connection);
#else
            await using var command = commandFunc.Invoke(connection);
#endif
            try
            {
                var dr = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);

                return await resultFunc.Invoke(dr, commandId, cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
            }
            finally
            {
#if NETSTANDARD
                connection.Close();
#else
                await connection
                    .CloseAsync()
                    .ConfigureAwait(ContinueOnCapturedContext);
#endif
            }
        }

        protected static DbConnection GetOpenConnection(IAmARelationalDbConnectionProvider connectionProvider)
        {
            var connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            return connection;
        }

        protected static async Task<DbConnection> GetOpenConnectionAsync(
            IAmARelationalDbConnectionProvider connectionProvider, CancellationToken cancellationToken)
        {
            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            return connection;
        }

        protected static void FinishWrite(DbConnection connection)
        {
            connection.Close();
        }

        private DbCommand CreateAddCommand(DbConnection connection, int inboxTimeout, IDbDataParameter[] parameters)
            // The caller has already populated the memoized probe via CausationColumnExists[Async](),
            // so this reads the cached result without a further round-trip; absent column → plain AddCommand.
            => CreateCommand(connection,
                GenerateSqlText(_causationColumnExists == true ? CausationQueries!.AddCausationCommand : queries.AddCommand),
                inboxTimeout, parameters);

        private DbCommand CreateExistsCommand(DbConnection connection, int inboxTimeout, IDbDataParameter[] parameters)
            => CreateCommand(connection, GenerateSqlText(queries.ExistsCommand), inboxTimeout, parameters);

        private DbCommand CreateGetCommand(DbConnection connection, int inboxTimeout, IDbDataParameter[] parameters)
            => CreateCommand(connection, GenerateSqlText(queries.GetCommand), inboxTimeout, parameters);

        protected virtual string GenerateSqlText(string sqlFormat, params string[] orderedParams)
            => string.Format(sqlFormat, orderedParams.Prepend(DatabaseConfiguration.InBoxTableName).ToArray());

        protected virtual DbCommand CreateCommand(DbConnection connection, string sqlText, int outBoxTimeout,
            params IDbDataParameter[] parameters)
        {

            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected abstract IDbDataParameter CreateSqlParameter(string parameterName, object? value);

        protected abstract IDbDataParameter CreateJsonSqlParameter(string parameterName, object? value);

        protected virtual IDbDataParameter[] CreateAddParameters<T>(T command, string contextKey)
            where T : class, IRequest
        {
            var body = 
                DatabaseConfiguration.JsonMessagePayload 
                    ? CreateJsonSqlParameter("@CommandBody", JsonSerializer.Serialize(command, JsonSerialisationOptions.Options)) :
                        DatabaseConfiguration.BinaryMessagePayload
                        ? CreateSqlParameter("@CommandBody", JsonSerializer.SerializeToUtf8Bytes(command, JsonSerialisationOptions.Options))
                        : CreateSqlParameter("@CommandBody", JsonSerializer.Serialize(command, JsonSerialisationOptions.Options));

            return
            [
                CreateSqlParameter("@CommandID", command.Id.Value),
                CreateSqlParameter("@CommandType", typeof(T).Name),
                body,
                CreateSqlParameter("@Timestamp", DateTime.UtcNow),
                CreateSqlParameter("@ContextKey", contextKey)
            ];
        }

        protected virtual IDbDataParameter[] CreateExistsParameters(string commandId, string contextKey)
        {
            return
            [
                CreateSqlParameter("@CommandID", commandId),
                CreateSqlParameter("@ContextKey", contextKey)
            ];
        }

        protected virtual IDbDataParameter[] CreateGetParameters(string commandId, string contextKey)
        {
            return
            [
                CreateSqlParameter("@CommandID", commandId),
                CreateSqlParameter("@ContextKey", contextKey)
            ];
        }

        protected virtual string CommandBodyColumnName => "CommandBody";
        protected virtual T MapFunction<T>(DbDataReader dr, string commandId) where T : class, IRequest
        {
            try
            {
                if (dr.Read())
                {
                    if (DatabaseConfiguration.BinaryMessagePayload)
                    {
                        var body = dr.GetFieldValue<byte[]>(dr.GetOrdinal(CommandBodyColumnName));
                        return JsonSerializer.Deserialize<T>(body, JsonSerialisationOptions.Options)!;
                    }
                    else
                    {
                        var body = dr.GetString(dr.GetOrdinal(CommandBodyColumnName));
                        return JsonSerializer.Deserialize<T>(body, JsonSerialisationOptions.Options)!;
                    }
                }
            }
            finally
            {
                dr.Close();
            }

            throw new RequestNotFoundException<T>(commandId);
        }

        protected virtual async Task<T> MapFunctionAsync<T>(DbDataReader dr, string commandId,
            CancellationToken cancellationToken) where T : class, IRequest
        {
            try
            {
                if (await dr.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                {
                    if (DatabaseConfiguration.BinaryMessagePayload)
                    {
                        var body = dr.GetFieldValue<byte[]>(dr.GetOrdinal(CommandBodyColumnName));
                        return JsonSerializer.Deserialize<T>(body, JsonSerialisationOptions.Options)!;
                    }
                    else
                    {
                        var body = dr.GetString(dr.GetOrdinal(CommandBodyColumnName));
                        return JsonSerializer.Deserialize<T>(body, JsonSerialisationOptions.Options)!;
                    }
                }
            }
            finally
            {

#if NETSTANDARD
                dr.Close();
#else
                await dr
                    .CloseAsync()
                    .ConfigureAwait(ContinueOnCapturedContext);
#endif
            }

            throw new RequestNotFoundException<T>(commandId);
        }

        protected virtual bool MapBoolFunction(DbDataReader dr, string commandId)
        {
            try
            {
                return dr.HasRows;
            }
            finally
            {
                dr.Close();
            }
        }

#if  NETSTANDARD
        protected virtual Task<bool> MapBoolFunctionAsync(DbDataReader dr, string commandId, CancellationToken cancellationToken)
        {
            try
            {
                return Task.FromResult(dr.HasRows);
            }
            finally
            {
                dr.Close();
            }
        }
#else
        protected virtual async Task<bool> MapBoolFunctionAsync(DbDataReader dr, string commandId,
            CancellationToken cancellationToken)
        {
            try
            {
                return dr.HasRows;
            }
            finally
            {
                await dr.CloseAsync().ConfigureAwait(ContinueOnCapturedContext);
            }
        }
#endif

        private string? MapCausationId(DbDataReader dr, string commandId)
        {
            try
            {
                if (dr.Read())
                {
                    return dr.IsDBNull(0) ? null : dr.GetString(0);
                }

                return null;
            }
            finally
            {
                dr.Close();
            }
        }

#if NETSTANDARD
        private async Task<string?> MapCausationIdAsync(DbDataReader dr, string commandId,
            CancellationToken cancellationToken)
        {
            try
            {
                if (await dr.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                {
                    return dr.IsDBNull(0) ? null : dr.GetString(0);
                }

                return null;
            }
            finally
            {
                dr.Close();
            }
        }
#else
        private async Task<string?> MapCausationIdAsync(DbDataReader dr, string commandId,
            CancellationToken cancellationToken)
        {
            try
            {
                if (await dr.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                {
                    return await dr.IsDBNullAsync(0, cancellationToken).ConfigureAwait(ContinueOnCapturedContext)
                        ? null
                        : dr.GetString(0);
                }

                return null;
            }
            finally
            {
                await dr.CloseAsync().ConfigureAwait(ContinueOnCapturedContext);
            }
        }
#endif

        // Reads the causation id from the request context bag, if present
        private static string? ReadCausationId(RequestContext? requestContext)
            => requestContext?.Bag.TryGetValue(RequestContextBagNames.CausationId, out var value) == true
                ? value as string
                : null;

        private static string ExtractSqlOperationName(string queryText)
        {
            return queryText.Split(' ')[0];
        }
    }
}
