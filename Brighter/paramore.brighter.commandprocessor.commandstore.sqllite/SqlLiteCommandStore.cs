// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.commandstore.mssql
// Author           : francesco.pighi
// Created          : 06-03-2015
//
// Last Modified By : ian cooper
// Last Modified On : 06-04-2015
// ***********************************************************************
// <copyright file="MsSqlCommandStore.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.commandstore.sqllite
{
    /// <summary>
    ///     Class SqlLiteCommandStore.
    /// </summary>
    public class SqlLiteCommandStore : IAmACommandStore, IAmACommandStoreAsync
    {
        private const int SqlliteDuplicateKeyError = 1555;
        private readonly SqlLiteCommandStoreConfiguration _configuration;
        private readonly ILog _log;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SqlLiteCommandStore" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public SqlLiteCommandStore(SqlLiteCommandStoreConfiguration configuration)
            : this(configuration, LogProvider.For<SqlLiteCommandStore>()) {}
        
        /// <summary>
        ///     Initializes a new instance of the <see cref="SqlLiteCommandStore" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="log">The log.</param>
        public SqlLiteCommandStore(SqlLiteCommandStoreConfiguration configuration, ILog log)
        {
            _configuration = configuration;
            _log = log;
            ContinueOnCapturedContext = false;
        }

        /// <summary>
        ///     Adds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <returns>Task.</returns>
        public void Add<T>(T command, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var parameters = InitAddDbParameters(command);

            using (var connection = GetConnection())
            {
                connection.Open();
                var sqlcmd = InitAddDbCommand(timeoutInMilliseconds, connection, parameters);
                try
                {
                    sqlcmd.ExecuteNonQuery();
                }
                catch (SqliteException sqliteException)
                {
                    if (sqliteException.SqliteErrorCode != SqlliteDuplicateKeyError) throw;
                    _log.WarnFormat(
                        "MsSqlMessageStore: A duplicate Command with the CommandId {0} was inserted into the Message Store, ignoring and continuing",
                        command.Id);
                }
            }
        }
        
        /// <summary>
        ///     Finds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <returns>T.</returns>
        public T Get<T>(Guid id, int timeoutInMilliseconds = -1) where T : class, IRequest, new()
        {
            var sql = string.Format("select * from {0} where CommandId = @commandId",
                _configuration.MessageStoreTableName);
            var parameters = new[]
            {
                CreateSqlParameter("CommandId", id)
            };

            return ExecuteCommand(command => ReadCommand<T>(command.ExecuteReader()), sql, timeoutInMilliseconds,
                parameters);
        }

        /// <summary>
        ///     Awaitably adds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="ct">Allow the sender to cancel the request, optional</param>
        /// <returns><see cref="Task" />.</returns>
        public async Task AddAsync<T>(T command, int timeoutInMilliseconds = -1, CancellationToken? ct = null)
            where T : class, IRequest
        {
            var parameters = InitAddDbParameters(command);

            using (var connection = GetConnection())
            {
                await connection.OpenAsync(ct ?? CancellationToken.None).ConfigureAwait(ContinueOnCapturedContext);
                var sqlcmd = InitAddDbCommand(timeoutInMilliseconds, connection, parameters);
                try
                {
                    await
                        sqlcmd.ExecuteNonQueryAsync(ct ?? CancellationToken.None)
                            .ConfigureAwait(ContinueOnCapturedContext);
                }
                catch (SqliteException sqliteException)
                {
                    if (sqliteException.SqliteErrorCode != SqlliteDuplicateKeyError) throw;
                    _log.WarnFormat(
                        "MsSqlMessageStore: A duplicate Command with the CommandId {0} was inserted into the Message Store, ignoring and continuing",
                        command.Id);
                }
            }
        }

        /// <summary>
        ///     If false we the default thread synchronization context to run any continuation, if true we re-use the original
        ///     synchronization context.
        ///     Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        ///     or access the Result or otherwise block. You may need the orginating synchronization context if you need to access
        ///     thread specific storage
        ///     such as HTTPContext
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///     Awaitably finds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="ct">Allow the sender to cancel the request</param>
        /// <returns><see cref="Task{T}" />.</returns>
        public async Task<T> GetAsync<T>(Guid id, int timeoutInMilliseconds = -1, CancellationToken? ct = null)
            where T : class, IRequest, new()
        {
            var sql = string.Format("select * from {0} where CommandId = @commandId",
                _configuration.MessageStoreTableName);
            var parameters = new[]
            {
                CreateSqlParameter("CommandId", id)
            };
            var result =
                await
                    ExecuteCommandAsync(
                        async command =>
                            ReadCommand<T>(
                                await
                                    command.ExecuteReaderAsync(ct ?? CancellationToken.None)
                                        .ConfigureAwait(ContinueOnCapturedContext)),
                        sql,
                        timeoutInMilliseconds,
                        ct,
                        parameters
                        )
                        .ConfigureAwait(ContinueOnCapturedContext);
            return result;
        }

        private DbParameter CreateSqlParameter(string parameterName, object value)
        {
            return new SqliteParameter(parameterName, value);
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
            CancellationToken? ct = null,
            params DbParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
                command.CommandText = sql;
                command.Parameters.AddRange(parameters);

                await connection.OpenAsync(ct ?? CancellationToken.None).ConfigureAwait(ContinueOnCapturedContext);
                var item = await execute(command).ConfigureAwait(ContinueOnCapturedContext);
                return item;
            }
        }

        private DbConnection GetConnection()
        {
            return new SqliteConnection(_configuration.ConnectionString);
        }

        private DbCommand InitAddDbCommand(int timeoutInMilliseconds, DbConnection connection, DbParameter[] parameters)
        {
            var sqlAdd =
                string.Format(
                    "insert into {0} (CommandID, CommandType, CommandBody, Timestamp) values (@CommandID, @CommandType, @CommandBody, @Timestamp)",
                    _configuration.MessageStoreTableName);

            var sqlcmd = connection.CreateCommand();
            if (timeoutInMilliseconds != -1) sqlcmd.CommandTimeout = timeoutInMilliseconds;

            sqlcmd.CommandText = sqlAdd;
            sqlcmd.Parameters.AddRange(parameters);
            return sqlcmd;
        }

        private DbParameter[] InitAddDbParameters<T>(T command) where T : class, IRequest
        {
            var commandJson = JsonConvert.SerializeObject(command);
            var parameters = new[]
            {
                CreateSqlParameter("CommandId", command.Id),
                CreateSqlParameter("CommandType", typeof (T).Name),
                CreateSqlParameter("CommandBody", commandJson),
                CreateSqlParameter("Timestamp", DateTime.UtcNow)
            };
            return parameters;
        }

        private TResult ReadCommand<TResult>(IDataReader dr) where TResult : class, IRequest, new()
        {
            if (dr.Read())
            {
                var body = dr.GetString(dr.GetOrdinal("CommandBody"));
                return JsonConvert.DeserializeObject<TResult>(body);
            }

            return new TResult {Id = Guid.Empty};
        }
    }
}