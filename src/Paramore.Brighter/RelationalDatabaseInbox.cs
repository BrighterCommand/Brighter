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
        : IAmAnInboxSync, IAmAnInboxAsync
    {
        protected IAmARelationalDatabaseConfiguration DatabaseConfiguration { get; } = configuration;

        protected IAmARelationalDbConnectionProvider ConnectionProvider { get; } = connectionProvider;

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
                { "db.operation.parameter.command.id", command.Id },
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
                { "db.operation.parameter.command.id", command.Id },
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

        protected abstract bool IsExceptionUniqueOrDuplicateIssue(Exception ex);

        protected virtual void WriteToStore(Func<DbConnection, DbCommand> commandFunc, Action? loggingAction)
        {
            var connection = GetOpenConnection(ConnectionProvider);

            using var command = commandFunc.Invoke(connection);
            try
            {
                command.ExecuteNonQuery();
            }
            catch (DbException exception)
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
            catch (DbException exception)
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
            => CreateCommand(connection, GenerateSqlText(queries.AddCommand), inboxTimeout, parameters);

        private DbCommand CreateExistsCommand(DbConnection connection, int inboxTimeout, IDbDataParameter[] parameters)
            => CreateCommand(connection, GenerateSqlText(queries.ExistsCommand), inboxTimeout, parameters);

        private DbCommand CreateGetCommand(DbConnection connection, int inboxTimeout, IDbDataParameter[] parameters)
            => CreateCommand(connection, GenerateSqlText(queries.GetCommand), inboxTimeout, parameters);

        private string GenerateSqlText(string sqlFormat, params string[] orderedParams)
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

        protected virtual IDbDataParameter[] CreateAddParameters<T>(T command, string contextKey)
            where T : class, IRequest
        {
            var commandJson = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
            return
            [
                CreateSqlParameter("@CommandID", command.Id.Value),
                CreateSqlParameter("@CommandType", typeof(T).Name),
                CreateSqlParameter("@CommandBody", commandJson),
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
                    var body = dr.GetString(dr.GetOrdinal(CommandBodyColumnName));
                    return JsonSerializer.Deserialize<T>(body, JsonSerialisationOptions.Options)!;
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
                    var body = dr.GetString(dr.GetOrdinal(CommandBodyColumnName));
                    return JsonSerializer.Deserialize<T>(body, JsonSerialisationOptions.Options)!;
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

        private static string ExtractSqlOperationName(string queryText)
        {
            return queryText.Split(' ')[0];
        }
    }
}
