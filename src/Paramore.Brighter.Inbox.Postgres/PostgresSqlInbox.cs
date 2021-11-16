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

namespace Paramore.Brighter.Inbox.Postgres
{
    public class PostgresSqlInbox : IAmAnInbox, IAmAnInboxAsync
    {
        private readonly PostgresSqlInboxConfiguration _configuration;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgresSqlInbox>();
        /// <summary>
        ///     If false we the default thread synchronization context to run any continuation, if true we re-use the original
        ///     synchronization context.
        ///     Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        ///     or access the Result or otherwise block. You may need the orginating synchronization context if you need to access
        ///     thread specific storage
        ///     such as HTTPContext
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }   
        
         public PostgresSqlInbox(PostgresSqlInboxConfiguration postgresSqlInboxConfiguration)
         {
             _configuration = postgresSqlInboxConfiguration;
             ContinueOnCapturedContext = false;
         }
 
        
        public void Add<T>(T command, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
              var parameters = InitAddDbParameters(command, contextKey);
  
              using (var connection = GetConnection())
              {
                  connection.Open();
                  var sqlcmd = InitAddDbCommand(connection, parameters, timeoutInMilliseconds);
                  try
                  {
                      sqlcmd.ExecuteNonQuery();
                  }
                  catch (PostgresException sqlException)
                  {
                      if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
                      {
                          s_logger.LogWarning(
                              "PostgresSqlOutbox: A duplicate Command with the CommandId {Id} was inserted into the Outbox, ignoring and continuing",
                              command.Id);
                          return;
                      }
  
                      throw;
                  }
              }
        }

        public T Get<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var sql = $"select * from {_configuration.InBoxTableName} where CommandId = @CommandId AND ContextKey = @ContextKey";
            var parameters = new[]
            {
                CreateNpgsqlParameter("CommandId", id),
                CreateNpgsqlParameter("ContextKey", contextKey)
            };

            return ExecuteCommand(command => ReadCommand<T>(command.ExecuteReader(), id), sql, timeoutInMilliseconds, parameters);
        }

        public bool Exists<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var sql = $"SELECT DISTINCT CommandId FROM {_configuration.InBoxTableName} WHERE CommandId = @CommandId AND ContextKey = @ContextKey FETCH FIRST 1 ROWS ONLY";
            var parameters = new[]
            {
                CreateNpgsqlParameter("CommandId", id),
                CreateNpgsqlParameter("ContextKey", contextKey)
            };

            return ExecuteCommand(command => command.ExecuteReader().HasRows, sql, timeoutInMilliseconds, parameters);
        }

        public async Task AddAsync<T>(T command, string contextKey, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var parameters = InitAddDbParameters(command, contextKey);

            using (var connection = GetConnection())
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                var sqlcmd = InitAddDbCommand(connection, parameters, timeoutInMilliseconds);
                try
                {
                    await sqlcmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                }
                catch (PostgresException sqlException)
                {
                    if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
                    {
                        s_logger.LogWarning(
                            "PostgresSqlOutbox: A duplicate Command with the CommandId {Id} was inserted into the Outbox, ignoring and continuing",
                            command.Id);
                        return;
                    }

                    throw;
                }
            }
        }

        public async Task<T> GetAsync<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var sql = $"select * from {_configuration.InBoxTableName} where CommandId = @CommandId AND ContextKey = @ContextKey";

            var parameters = new[]
            {
                CreateNpgsqlParameter("CommandId", id),
                CreateNpgsqlParameter("ContextKey", contextKey)
            };

            return await ExecuteCommandAsync(
                    async command => ReadCommand<T>(await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext), id),
                    sql,
                    timeoutInMilliseconds,
                    cancellationToken,
                    parameters)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        public async Task<bool> ExistsAsync<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var sql = $"SELECT DISTINCT CommandId FROM {_configuration.InBoxTableName} WHERE CommandId = @CommandId AND ContextKey = @ContextKey FETCH FIRST 1 ROWS ONLY";
            var parameters = new[]
            {
                CreateNpgsqlParameter("CommandId", id),
                CreateNpgsqlParameter("ContextKey", contextKey)
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
        
        private NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_configuration.ConnectionString);
        }
        
        private NpgsqlParameter CreateNpgsqlParameter(string parametername, object value)
        {
            if (value != null)
                return new NpgsqlParameter(parametername, value);
            else
                return new NpgsqlParameter(parametername, DBNull.Value);
        }

        private DbCommand InitAddDbCommand(DbConnection connection, DbParameter[] parameters, int timeoutInMilliseconds)
        {
            var command = connection.CreateCommand();
            var sql = string.Format(
                "INSERT INTO {0} (CommandID, CommandType, CommandBody, Timestamp, ContextKey) VALUES (@CommandID, @CommandType, @CommandBody, @Timestamp, @ContextKey)",
                _configuration.InBoxTableName);
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
            return command;
        }

        private DbParameter[] InitAddDbParameters<T>(T command, string contextKey) where T : class, IRequest
        {
            var commandJson = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
            var parameters = new[]
            {
                CreateNpgsqlParameter("CommandID", command.Id),
                CreateNpgsqlParameter("CommandType", typeof (T).Name),
                CreateNpgsqlParameter("CommandBody", commandJson),
                new NpgsqlParameter("Timestamp", NpgsqlDbType.TimestampTz) {Value = DateTimeOffset.UtcNow},
                CreateNpgsqlParameter("ContextKey", contextKey)
            };
            return parameters;
        }

        private T ExecuteCommand<T>(Func<DbCommand, T> execute, string sql, int timeoutInMilliseconds,
            params DbParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
                command.CommandText = sql;
                command.Parameters.AddRange(parameters);

                connection.Open();
                var item = execute(command);
                return item;
            }
        }

        private async Task<T> ExecuteCommandAsync<T>(
            Func<DbCommand, Task<T>> execute,
            string sql,
            int timeoutInMilliseconds,
            CancellationToken cancellationToken = default(CancellationToken),
            params DbParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
                command.CommandText = sql;
                command.Parameters.AddRange(parameters);

                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                var item = await execute(command).ConfigureAwait(ContinueOnCapturedContext);
                return item;
            }
        }
        
        private TResult ReadCommand<TResult>(IDataReader dr, Guid commandId) where TResult : class, IRequest
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
