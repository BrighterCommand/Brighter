#region Licence

/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian.cooper@yahoo.co.uk>

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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Inbox.Postgres
{
    public partial class PostgreSqlInbox : IAmAnInboxSync, IAmAnInboxAsync
    {
        private readonly IAmARelationalDatabaseConfiguration _configuration;
        private readonly IAmARelationalDbConnectionProvider _connectionProvider;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgreSqlInbox>();
        /// <summary>
        ///     If false we the default thread synchronization context to run any continuation, if true we re-use the original
        ///     synchronization context.
        ///     Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        ///     or access the Result or otherwise block. You may need the originating synchronization context if you need to access
        ///     thread specific storage
        ///     such as HTTPContext
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        public PostgreSqlInbox(IAmARelationalDatabaseConfiguration configuration, IAmARelationalDbConnectionProvider connectionProvider = null)
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
            ContinueOnCapturedContext = false;
        }

        public void Add<T>(T command, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var parameters = InitAddDbParameters(command, contextKey);
            var connection = GetConnection(); 
            try
            {
                using var sqlcmd = InitAddDbCommand(connection, parameters, timeoutInMilliseconds);
                sqlcmd.ExecuteNonQuery();
            }
            catch (PostgresException sqlException)
            {
                if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    Log.DuplicateCommandWarning(s_logger, command.Id);
                    return;
                }
                throw;
            }
            finally
            {
                    connection.Dispose();
            }
        }

        public T Get<T>(string id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var sql = $"SELECT * FROM {_configuration.InBoxTableName} WHERE CommandId = @CommandId AND ContextKey = @ContextKey";
            var parameters = new[]
            {
                InitNpgsqlParameter("CommandId", id),
                InitNpgsqlParameter("ContextKey", contextKey)
            };

            return ExecuteCommand(command => ReadCommand<T>(command.ExecuteReader(), id), sql, timeoutInMilliseconds, parameters);
        }

        public bool Exists<T>(string id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var sql = $"SELECT DISTINCT CommandId FROM {_configuration.InBoxTableName} WHERE CommandId = @CommandId AND ContextKey = @ContextKey FETCH FIRST 1 ROWS ONLY";
            var parameters = new[]
            {
                InitNpgsqlParameter("CommandId", id),
                InitNpgsqlParameter("ContextKey", contextKey)
            };

            return ExecuteCommand(command => command.ExecuteReader().HasRows, sql, timeoutInMilliseconds, parameters);
        }

        public async Task AddAsync<T>(T command, string contextKey, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default) where T : class, IRequest
        {
            var parameters = InitAddDbParameters(command, contextKey);
            var connection = GetConnection(); 

            try
            {
                using var sqlcmd = InitAddDbCommand(connection, parameters, timeoutInMilliseconds);
                await sqlcmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            }
            catch (PostgresException sqlException)
            {
                if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    Log.DuplicateCommandWarning(s_logger, command.Id);
                    return;
                }

                throw;
            }
            finally
            {
                connection.Dispose();
            }
        }

        public async Task<T> GetAsync<T>(string id, string contextKey, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default) where T : class, IRequest
        {
            var sql = $"SELECT * FROM {_configuration.InBoxTableName} WHERE CommandId = @CommandId AND ContextKey = @ContextKey";

            var parameters = new[]
            {
                InitNpgsqlParameter("CommandId", id),
                InitNpgsqlParameter("ContextKey", contextKey)
            };

            return await ExecuteCommandAsync(
                    async command => ReadCommand<T>(await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext), id),
                    sql,
                    timeoutInMilliseconds,
                    cancellationToken,
                    parameters)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        public async Task<bool> ExistsAsync<T>(string id, string contextKey, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default) where T : class, IRequest
        {
            var sql = $"SELECT DISTINCT CommandId FROM {_configuration.InBoxTableName} WHERE CommandId = @CommandId AND ContextKey = @ContextKey FETCH FIRST 1 ROWS ONLY";
            var parameters = new[]
            {
                InitNpgsqlParameter("CommandId", id),
                InitNpgsqlParameter("ContextKey", contextKey)
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
        
        private DbConnection GetConnection()
        {
            var connection = new NpgsqlConnection(_configuration.ConnectionString);
            connection.Open();
            return connection;
        }

        private async Task<DbConnection> GetOpenConnectionAsync(IAmARelationalDbConnectionProvider connectionProvider, CancellationToken cancellationToken = default)
        {
            DbConnection connection = await connectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            return connection;
        }

        private NpgsqlParameter InitNpgsqlParameter(string parametername, object value)
        {
            if (value != null)
                return new NpgsqlParameter(parametername, value);
            else
                return new NpgsqlParameter(parametername, DBNull.Value);
        }

        private DbCommand InitAddDbCommand(DbConnection connection, DbParameter[] parameters, int timeoutInMilliseconds)
        {
            var command = connection.CreateCommand();
            command.CommandText = string.Format(
                "INSERT INTO {0} (CommandID, CommandType, CommandBody, Timestamp, ContextKey) VALUES (@CommandID, @CommandType, @CommandBody, @Timestamp, @ContextKey)",
                _configuration.InBoxTableName);
            command.Parameters.AddRange(parameters);
            return command;
        }

        private DbParameter[] InitAddDbParameters<T>(T command, string contextKey) where T : class, IRequest
        {
            var commandJson = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
            var parameters = new[]
            {
                InitNpgsqlParameter("CommandID", command.Id),
                InitNpgsqlParameter("CommandType", typeof (T).Name),
                InitNpgsqlParameter("CommandBody", commandJson),
                new NpgsqlParameter("Timestamp", NpgsqlDbType.TimestampTz) {Value = DateTimeOffset.UtcNow},
                InitNpgsqlParameter("ContextKey", contextKey)
            };
            return parameters;
        }

        private T ExecuteCommand<T>(Func<DbCommand, T> execute, string sql, int timeoutInMilliseconds,
            params DbParameter[] parameters)
        {
            var connection = GetConnection(); 

            try
            {
                using var command = connection.CreateCommand();
                if (timeoutInMilliseconds != -1)
                    command.CommandTimeout = timeoutInMilliseconds;

                command.CommandText = sql;
                command.Parameters.AddRange(parameters);

                return execute(command);
            }
            finally
            {
                connection.Dispose();
            }
        }

        private async Task<T> ExecuteCommandAsync<T>(
            Func<DbCommand, Task<T>> execute,
            string sql,
            int timeoutInMilliseconds,
            CancellationToken cancellationToken = default,
            params DbParameter[] parameters)
        {
            var connection = GetConnection(); 

            try
            {
                using var command = connection.CreateCommand();
                if (timeoutInMilliseconds != -1)
                    command.CommandTimeout = timeoutInMilliseconds;

                command.CommandText = sql;
                command.Parameters.AddRange(parameters);

                return await execute(command).ConfigureAwait(ContinueOnCapturedContext);
            }
            finally
            {
                connection.Dispose();
            }
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

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "PostgresSqlOutbox: A duplicate Command with the CommandId {Id} was inserted into the Outbox, ignoring and continuing")]
            public static partial void DuplicateCommandWarning(ILogger logger, string id);
        }
    }
}

