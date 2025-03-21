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
                connection => CreateAddCommand(connection, parameters),
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
                MapFunction<T>);
        }

        public bool Exists<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds)
            where T : class, IRequest
        {
            var parameters = CreateExistsParameters(id, contextKey);
            return ReadFromStore(
                connection => CreateExistsCommand(connection, timeoutInMilliseconds, parameters),
                MapBoolFunction);
        }

        public async Task AddAsync<T>(T command, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds, CancellationToken cancellationToken)
            where T : class, IRequest
        {
            var parameters = CreateAddParameters(command, contextKey);
            await WriteToStoreAsync(
                connection => CreateAddCommand(connection, parameters),
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
                cancellationToken);
        }

        public async Task<bool> ExistsAsync<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds, CancellationToken cancellationToken)
            where T : class, IRequest
        {
            var parameters = CreateExistsParameters(id, contextKey);
            return await ReadFromStoreAsync(
                connection => CreateExistsCommand(connection, timeoutInMilliseconds, parameters),
                MapBoolFunctionAsync,
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
            Func<DbDataReader, T> resultFunc
        );

        protected abstract Task<T> ReadFromStoreAsync<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, CancellationToken, Task<T>> resultFunc,
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

        private DbCommand CreateAddCommand(DbConnection connection, IDbDataParameter[] parameters)
            => CreateCommand(connection, GenerateSqlText(queries.AddCommand), 0, parameters);

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

        protected abstract T MapFunction<T>(DbDataReader dr) where T : class, IRequest;

        protected abstract Task<T> MapFunctionAsync<T>(DbDataReader dr, CancellationToken cancellationToken) where T : class, IRequest;

        protected abstract bool MapBoolFunction(DbDataReader dr);

        protected abstract Task<bool> MapBoolFunctionAsync(DbDataReader dr, CancellationToken cancellationToken);
    }
}
