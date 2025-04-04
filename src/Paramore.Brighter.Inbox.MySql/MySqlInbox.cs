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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Inbox.MySql
{
    /// <summary>
    ///     Class MySqlInbox.
    /// </summary>
    public class MySqlInbox : RelationalDatabaseInbox
    {
        private const int MySqlDuplicateKeyError = 1062;
        private readonly IAmARelationalDbConnectionProvider _connectionProvider;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MySqlInbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="connectionProvider">The Connection Provider.</param>
        public MySqlInbox(IAmARelationalDatabaseConfiguration configuration, IAmARelationalDbConnectionProvider connectionProvider)
            : base(DbSystem.MySql, configuration.DatabaseName, configuration.InBoxTableName, 
                  new MySqlQueries(), ApplicationLogging.CreateLogger<MySqlInbox>())
        {
            ContinueOnCapturedContext = false;
            _connectionProvider = connectionProvider;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="MySqlInbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public MySqlInbox(IAmARelationalDatabaseConfiguration configuration) : this(configuration,
            new MySqlConnectionProvider(configuration))
        {
        }

        protected override DbCommand CreateCommand(
            DbConnection connection, string sqlText, int outBoxTimeout, params IDbDataParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected override void WriteToStore(Func<DbConnection, DbCommand> commandFunc, Action loggingAction)
        {
            using var connection = GetOpenConnection(_connectionProvider);
            using var command = commandFunc.Invoke(connection);
            try
            {
                command.ExecuteNonQuery();
            }
            catch (MySqlException ex)
            {
                if (ex.Number == MySqlDuplicateKeyError)
                {
                    loggingAction.Invoke();
                    return;
                }

                throw;
            }
        }

        protected override async Task WriteToStoreAsync(Func<DbConnection, DbCommand> commandFunc,
            Action loggingAction, CancellationToken cancellationToken)
        {
            using var connection = await GetOpenConnectionAsync(_connectionProvider, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            using var command = commandFunc.Invoke(connection);
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            }
            catch (MySqlException ex)
            {
                if (ex.Number == MySqlDuplicateKeyError)
                {
                    loggingAction.Invoke();
                    return;
                }

                throw;
            }
        }

        protected override T ReadFromStore<T>(Func<DbConnection, DbCommand> commandFunc, Func<DbDataReader, string, T> resultFunc, string commandId)
        {
            using var connection = _connectionProvider.GetConnection();
            using var command = commandFunc.Invoke(connection);

            var result = command.ExecuteReader();
            return resultFunc.Invoke(result, commandId);
        }

        protected override async Task<T> ReadFromStoreAsync<T>(Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, string, CancellationToken, Task<T>> resultFunc,
            string commandId,
            CancellationToken cancellationToken)
        {
            using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            using var command = commandFunc.Invoke(connection);

            var result = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            return await resultFunc.Invoke(result, commandId, cancellationToken);
        }

        protected override IDbDataParameter[] CreateAddParameters<T>(T command, string contextKey)
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

        protected override IDbDataParameter[] CreateGetParameters(string commandId, string contextKey)
        {
            var parameters = new[]
            {
                CreateSqlParameter("CommandID", commandId),
                CreateSqlParameter("ContextKey", contextKey)
            };
            return parameters;
        }

        protected override IDbDataParameter[] CreateExistsParameters(string commandId, string contextKey)
        {
            var parameters = new[]
            {
                CreateSqlParameter("CommandID", commandId),
                CreateSqlParameter("ContextKey", contextKey)
            };
            return parameters;
        }

        private DbParameter CreateSqlParameter(string parameterName, object value)
        {
            return new MySqlParameter
            {
                ParameterName = parameterName,
                Value = value
            };
        }

        protected override T MapFunction<T>(DbDataReader dr, string commandId)
        {
            try
            {
                if (dr.Read())
                {
                    var body = dr.GetString(dr.GetOrdinal("CommandBody"));
                    return JsonSerializer.Deserialize<T>(body, JsonSerialisationOptions.Options);
                }
            }
            finally
            {
                dr.Close();
            }

            throw new RequestNotFoundException<T>(commandId);
        }

        protected override async Task<T> MapFunctionAsync<T>(DbDataReader dr, string commandId,
            CancellationToken cancellationToken)
        {
            try
            {
                if (await dr.ReadAsync().ConfigureAwait(ContinueOnCapturedContext))
                {
                    var body = dr.GetString(dr.GetOrdinal("CommandBody"));
                    return JsonSerializer.Deserialize<T>(body, JsonSerialisationOptions.Options);
                }
            }
            finally
            {
#if NETSTANDARD2_0
                dr.Close();
#else
                await dr.CloseAsync().ConfigureAwait(ContinueOnCapturedContext);
#endif
            }

            throw new RequestNotFoundException<T>(commandId);
        }

        protected override bool MapBoolFunction(DbDataReader dr, string commandId)
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

        protected override Task<bool> MapBoolFunctionAsync(DbDataReader dr, string commandId,
            CancellationToken cancellationToken)
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
    }
}

