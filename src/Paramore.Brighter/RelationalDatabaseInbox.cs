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
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    public abstract class RelationalDatabaseInbox(
        string outboxTableName,
        IRelationalDatabaseInboxQueries queries,
        ILogger logger)
        : IAmAnInboxSync, IAmAnInboxAsync
    {
        /// <inheritdoc/>
        public bool ContinueOnCapturedContext { get; set; }

        /// <inheritdoc/>
        public IAmABrighterTracer? Tracer { private get; set; }

        public void Add<T>(T command, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds)
            where T : class, IRequest
        {
            var parameters = CreateAddParameters(command, contextKey);
            WriteToStore(
                connection => CreateAddCommand(connection, timeoutInMilliseconds, parameters),
                () =>
                {
                    logger.LogWarning("Inbox: A duplicate command with the ID {Id} was inserted into the Inbox, ignoring and continuing",
                        command.Id);
                });
        }

        public T Get<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds)
            where T : class, IRequest
        {
            var parameters = CreateGetParameters(id, contextKey);
            return ReadFromStore(
                connection => CreateGetCommand(connection, timeoutInMilliseconds, parameters), 
                MapFunction<T>,
                id);
        }

        public bool Exists<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds)
            where T : class, IRequest
        {
            var parameters = CreateExistsParameters(id, contextKey);
            return ReadFromStore(
                connection => CreateExistsCommand(connection, timeoutInMilliseconds, parameters),
                MapBoolFunction,
                id);
        }

        public async Task AddAsync<T>(T command, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds, CancellationToken cancellationToken)
            where T : class, IRequest
        {
            var parameters = CreateAddParameters(command, contextKey);
            await WriteToStoreAsync(
                connection => CreateAddCommand(connection, timeoutInMilliseconds, parameters),
                () =>
                {
                    logger.LogWarning("Inbox: A duplicate command with the ID {Id} was inserted into the Inbox, ignoring and continuing",
                        command.Id);
                },
                cancellationToken);
        }

        public async Task<T> GetAsync<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds, CancellationToken cancellationToken)
            where T : class, IRequest
        {
            var parameters = CreateGetParameters(id, contextKey);
            return await ReadFromStoreAsync(
                connection => CreateGetCommand(connection, timeoutInMilliseconds, parameters),
                MapFunctionAsync<T>,
                id,
                cancellationToken);
        }

        public async Task<bool> ExistsAsync<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds, CancellationToken cancellationToken)
            where T : class, IRequest
        {
            var parameters = CreateExistsParameters(id, contextKey);
            return await ReadFromStoreAsync(
                connection => CreateExistsCommand(connection, timeoutInMilliseconds, parameters),
                MapBoolFunctionAsync,
                id,
                cancellationToken);
        }

        protected abstract void WriteToStore(
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction
        );

        protected abstract Task WriteToStoreAsync(
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction,
            CancellationToken cancellationToken
        );

        protected abstract T ReadFromStore<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, string, T> resultFunc,
            string commandId
        );

        protected abstract Task<T> ReadFromStoreAsync<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, string, CancellationToken, Task<T>> resultFunc,
            string commandId,
            CancellationToken cancellationToken
        );

        protected DbConnection GetOpenConnection(IAmARelationalDbConnectionProvider connectionProvider)
        {
            var connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                connection.Open();

            return connection;
        }

        protected async Task<DbConnection> GetOpenConnectionAsync(
            IAmARelationalDbConnectionProvider connectionProvider, CancellationToken cancellationToken)
        {
            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            return connection;
        }

        protected void FinishWrite(DbConnection connection)
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
            => string.Format(sqlFormat, orderedParams.Prepend(outboxTableName).ToArray());

        protected abstract DbCommand CreateCommand(DbConnection connection, string sqlText, int outBoxTimeout,
            params IDbDataParameter[] parameters);

        protected abstract IDbDataParameter[] CreateAddParameters<T>(T command, string contextKey) where T : class, IRequest;

        protected abstract IDbDataParameter[] CreateExistsParameters(string commandId, string contextKey);

        protected abstract IDbDataParameter[] CreateGetParameters(string commandId, string contextKey);

        protected abstract T MapFunction<T>(DbDataReader dr, string commandId) where T : class, IRequest;

        protected abstract Task<T> MapFunctionAsync<T>(DbDataReader dr, string commandId, CancellationToken cancellationToken) where T : class, IRequest;

        protected abstract bool MapBoolFunction(DbDataReader dr, string commandId);

        protected abstract Task<bool> MapBoolFunctionAsync(DbDataReader dr, string commandId, CancellationToken cancellationToken);
    }
}
