﻿#region Licence

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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Inbox.MySql
{
    /// <summary>
    ///     Class MySqlInbox.
    /// </summary>
    public class MySqlInbox : IAmAnInboxSync, IAmAnInboxAsync
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MySqlInbox>();

        private const int MySqlDuplicateKeyError = 1062;
        private readonly IAmARelationalDatabaseConfiguration _configuration;

        /// <inheritdoc />
        public IAmABrighterTracer Tracer { private get; set; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="MySqlInbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public MySqlInbox(IAmARelationalDatabaseConfiguration configuration)
        {
            _configuration = configuration;
            ContinueOnCapturedContext = false;
        }

        /// <inheritdoc />
        public void Add<T>(T command, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var parameters = InitAddDbParameters(command, contextKey);

            using var connection = GetConnection();
            connection.Open();
            var sqlcmd = InitAddDbCommand(connection, parameters, timeoutInMilliseconds);
            try
            {
                sqlcmd.ExecuteNonQuery();
            }
            catch (MySqlException sqlException)
            {
                if (sqlException.Number == MySqlDuplicateKeyError)
                {
                    s_logger.LogWarning(
                        "MySqlOutbox: A duplicate Command with the CommandId {Id} was inserted into the Outbox, ignoring and continuing",
                        command.Id);
                    return;
                }

                throw;
            }
        }

        /// <inheritdoc />
        public T Get<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var sql = $"select * from {_configuration.InBoxTableName} where CommandId = @commandId and ContextKey = @contextKey";
            var parameters = new[]
            {
                CreateSqlParameter("CommandId", id),
                CreateSqlParameter("ContextKey", contextKey)
            };

            return ExecuteCommand(command => ReadCommand<T>(command.ExecuteReader(), id), sql, timeoutInMilliseconds,
                parameters);
        }

        /// <inheritdoc />
        public bool Exists<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var sql = $"SELECT CommandId FROM {_configuration.InBoxTableName} WHERE CommandId = @commandId and ContextKey = @contextKey LIMIT 1";
            var parameters = new[]
            {
                CreateSqlParameter("CommandId", id),
                CreateSqlParameter("ContextKey", contextKey)
            };

            return ExecuteCommand(command => command.ExecuteReader().HasRows, sql, timeoutInMilliseconds,
                parameters);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default) where T : class, IRequest
        {
            var sql = $"SELECT CommandId FROM {_configuration.InBoxTableName} WHERE CommandId = @commandId and ContextKey = @contextKey LIMIT 1";
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

        /// <inheritdoc />
        public async Task AddAsync<T>(T command, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default)
            where T : class, IRequest
        {
            var parameters = InitAddDbParameters(command, contextKey);

            using var connection = GetConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            var sqlcmd = InitAddDbCommand(connection, parameters, timeoutInMilliseconds);
            try
            {
                await sqlcmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            }
            catch (MySqlException sqlException)
            {
                if (sqlException.Number == MySqlDuplicateKeyError)
                {
                    s_logger.LogWarning(
                        "MySqlOutbox: A duplicate Command with the CommandId {Id} was inserted into the Outbox, ignoring and continuing",
                        command.Id);
                    return;
                }

                throw;
            }
        }

        /// <inheritdoc />
        public bool ContinueOnCapturedContext { get; set; }

        /// <inheritdoc />
        public async Task<T> GetAsync<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default)
            where T : class, IRequest
        {
            var sql = $"select * from {_configuration.InBoxTableName} where CommandId = @commandId and ContextKey = @contextKey";

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

        private DbParameter CreateSqlParameter(string parameterName, object value)
        {
            return new MySqlParameter
            {
                ParameterName = parameterName,
                Value = value
            };
        }

        private T ExecuteCommand<T>(Func<DbCommand, T> execute, string sql, int timeoutInMilliseconds,
            params DbParameter[] parameters)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);

            connection.Open();
            var item = execute(command);
            return item;
        }

        private async Task<T> ExecuteCommandAsync<T>(
            Func<DbCommand, Task<T>> execute,
            string sql,
            int timeoutInMilliseconds,
            CancellationToken cancellationToken = default,
            params DbParameter[] parameters)
        {
            using var connection = GetConnection();
            using var command = connection.CreateCommand();
            if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);

            await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            var item = await execute(command).ConfigureAwait(ContinueOnCapturedContext);
            return item;
        }

        private DbConnection GetConnection()
        {
            return new MySqlConnection(_configuration.ConnectionString);
        }

        private DbCommand InitAddDbCommand(DbConnection connection, DbParameter[] parameters, int timeoutInMilliseconds)
        {
            var sqlAdd =
                $"insert into {_configuration.InBoxTableName} (CommandID, CommandType, CommandBody, Timestamp, ContextKey) values (@CommandID, @CommandType, @CommandBody, @Timestamp, @ContextKey)";

            var sqlcmd = connection.CreateCommand();
            if (timeoutInMilliseconds != -1) sqlcmd.CommandTimeout = timeoutInMilliseconds;

            sqlcmd.CommandText = sqlAdd;
            sqlcmd.Parameters.AddRange(parameters);
            return sqlcmd;
        }

        private DbParameter[] InitAddDbParameters<T>(T command, string contextKey) where T : class, IRequest
        {
            var commandJson = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
            var parameters = new[]
            {
                CreateSqlParameter("CommandID", command.Id),
                CreateSqlParameter("CommandType", typeof (T).Name),
                CreateSqlParameter("CommandBody", commandJson),
                CreateSqlParameter("Timestamp", DateTime.UtcNow),
                CreateSqlParameter("ContextKey", contextKey),
            };
            return parameters;
        }

        private TResult ReadCommand<TResult>(IDataReader dr, string id) where TResult : class, IRequest
        {
            if (dr.Read())
            {
                var body = dr.GetString(dr.GetOrdinal("CommandBody"));
                return JsonSerializer.Deserialize<TResult>(body, JsonSerialisationOptions.Options);
            }

            throw new RequestNotFoundException<TResult>(id);
        }
    }
}
