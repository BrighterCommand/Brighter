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
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Inbox.MsSql
{
    /// <summary>
    ///     Class MsSqlInbox.
    /// </summary>
    public class MsSqlInbox : IAmAnInboxSync, IAmAnInboxAsync
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlInbox>();

        private const int MsSqlDuplicateKeyError_UniqueIndexViolation = 2601;
        private const int MsSqlDuplicateKeyError_UniqueConstraintViolation = 2627;
        private readonly IAmARelationalDatabaseConfiguration _configuration;
        private readonly IAmARelationalDbConnectionProvider _connectionProvider;

        /// <inheritdoc />
        public IAmABrighterTracer Tracer { private get; set; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlInbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="connectionProvider">The Connection Provider.</param>
        public MsSqlInbox(IAmARelationalDatabaseConfiguration configuration, IAmARelationalDbConnectionProvider connectionProvider)
        {
            _configuration = configuration;
            ContinueOnCapturedContext = false;
            _connectionProvider = connectionProvider;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlInbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public MsSqlInbox(IAmARelationalDatabaseConfiguration configuration) : this(configuration,
            new MsSqlConnectionProvider(configuration))
        {
        }

        /// <inheritdoc/>
        public void Add<T>(T command, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var parameters = InitAddDbParameters(command, contextKey);

            using var connection = _connectionProvider.GetConnection();
            var sqlcmd = InitAddDbCommand(connection, parameters, timeoutInMilliseconds);
            try
            {
                sqlcmd.ExecuteNonQuery();
            }
            catch (SqlException sqlException)
            {
                if (sqlException.Number == MsSqlDuplicateKeyError_UniqueIndexViolation || sqlException.Number == MsSqlDuplicateKeyError_UniqueConstraintViolation)
                {
                    s_logger.LogWarning(
                        "MsSqlOutbox: A duplicate Command with the CommandId {Id} was inserted into the Outbox, ignoring and continuing",
                        command.Id);
                    return;
                }

                throw;
            }
        }

        /// <inheritdoc/>
        public T Get<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var sql = $"select * from {_configuration.InBoxTableName} where CommandId = @commandId AND ContextKey = @contextKey";
            var parameters = new[]
            {
                CreateSqlParameter("CommandId", id),
                CreateSqlParameter("ContextKey", contextKey)
            };

            return ExecuteCommand(command => ReadCommand<T>(command.ExecuteReader(), id), sql, timeoutInMilliseconds, parameters);
        }

        /// <inheritdoc/>
        public bool Exists<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var sql = $"SELECT TOP 1 CommandId FROM {_configuration.InBoxTableName} WHERE CommandId = @commandId AND ContextKey = @contextKey";
            var parameters = new[]
            {
                CreateSqlParameter("CommandId", id),
                CreateSqlParameter("ContextKey", contextKey)
            };

            return ExecuteCommand(command => command.ExecuteReader().HasRows, sql, timeoutInMilliseconds, parameters);
        }

        /// <inheritdoc/>
        public async Task AddAsync<T>(T command, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default)
            where T : class, IRequest
        {
            var parameters = InitAddDbParameters(command, contextKey);

            using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            var sqlcmd = InitAddDbCommand(connection, parameters, timeoutInMilliseconds);
            try
            {
                await sqlcmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            }
            catch (SqlException sqlException)
            {
                if (sqlException.Number == MsSqlDuplicateKeyError_UniqueIndexViolation || sqlException.Number == MsSqlDuplicateKeyError_UniqueConstraintViolation)
                {
                    s_logger.LogWarning(
                        "MsSqlOutbox: A duplicate Command with the CommandId {Id} was inserted into the Outbox, ignoring and continuing",
                        command.Id);
                    return;
                }

                throw;
            }
        }


        /// <inheritdoc/>
        public async Task<bool> ExistsAsync<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default) where T : class, IRequest
        {
            var sql = $"SELECT TOP 1 CommandId FROM {_configuration.InBoxTableName} WHERE CommandId = @commandId AND ContextKey = @contextKey";
            var parameters = new[]
            {
                CreateSqlParameter("CommandId", id),
                CreateSqlParameter("ContextKey", contextKey)
            };

            return await ExecuteCommandAsync<bool>(
                    async command =>
                    {
                        var reader = await command.ExecuteReaderAsync(cancellationToken);
                        return reader.HasRows;
                    },
                    sql,
                    timeoutInMilliseconds,
                    cancellationToken,
                    parameters)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        /// <inheritdoc/>
        public bool ContinueOnCapturedContext { get; set; }

        /// <inheritdoc/>
        public async Task<T> GetAsync<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default)
            where T : class, IRequest
        {
            var sql = $"select * from {_configuration.InBoxTableName} where CommandId = @commandId AND ContextKey = @contextKey";

            var parameters = new[]
            {
                CreateSqlParameter("CommandId", id),
                CreateSqlParameter("ContextKey", contextKey)
            };

            return await ExecuteCommandAsync(
                async command => ReadCommand<T>(await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext), id),
                sql,
                timeoutInMilliseconds,
                cancellationToken,
                parameters)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        private SqlParameter CreateSqlParameter(string parameterName, object value)
        {
            return new SqlParameter(parameterName, value ?? DBNull.Value);
        }

        private T ExecuteCommand<T>(
            Func<DbCommand, T> execute, 
            string sql, 
            int timeoutInMilliseconds,
            params IDbDataParameter[] parameters
            )
        {
            using var connection = _connectionProvider.GetConnection();
            using var command = connection.CreateCommand();
            if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);

            var item = execute(command);
            return item;
        }

        private async Task<T> ExecuteCommandAsync<T>(
            Func<DbCommand, Task<T>> execute,
            string sql,
            int timeoutInMilliseconds,
            CancellationToken cancellationToken = default,
            params IDbDataParameter[] parameters)
        {
            using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            using var command = connection.CreateCommand();
            if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);

            var item = await execute(command).ConfigureAwait(ContinueOnCapturedContext);
            return item;
        }

        private DbCommand InitAddDbCommand(DbConnection connection, IDbDataParameter[] parameters, int timeoutInMilliseconds)
        {
            var sqlAdd =
                $"insert into {_configuration.InBoxTableName} (CommandID, CommandType, CommandBody, Timestamp, ContextKey) values (@CommandID, @CommandType, @CommandBody, @Timestamp, @ContextKey)";

            var sqlcmd = connection.CreateCommand();
            if (timeoutInMilliseconds != -1) sqlcmd.CommandTimeout = timeoutInMilliseconds;

            sqlcmd.CommandText = sqlAdd;
            sqlcmd.Parameters.AddRange(parameters);
            return sqlcmd;
        }

        private IDbDataParameter[] InitAddDbParameters<T>(T command, string contextKey) where T : class, IRequest
        {
            var commandJson = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
            var parameters = new[]
            {
                CreateSqlParameter("CommandID", command.Id),
                CreateSqlParameter("CommandType", typeof (T).Name),
                CreateSqlParameter("CommandBody", commandJson),
                CreateSqlParameter("Timestamp", DateTime.UtcNow),
                CreateSqlParameter("ContextKey", contextKey)
            };
            return parameters;
        }

        private TResult ReadCommand<TResult>(IDataReader dr, string commandId) where TResult : class, IRequest
        {
            if (dr.Read())
            {
                var body = dr.GetString(dr.GetOrdinal("CommandBody"));
                return JsonSerializer.Deserialize<TResult>(body, JsonSerialisationOptions.Options);
            }

            throw new RequestNotFoundException<TResult>(commandId);
        }
    }
}
