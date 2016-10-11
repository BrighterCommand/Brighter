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
using System.Linq;
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

        public void Add<T>(T command, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var parameters = InitAddDbParameters(command, this);

            using (var connection = this.GetConnection())
            {
                connection.Open();
                var sqlcmd = InitAddDbCommand(timeoutInMilliseconds, connection, parameters, this);
                try
                {
                    sqlcmd.ExecuteNonQuery();
                }
                catch (SqliteException sqliteException)
                {
                    if (sqliteException.SqliteErrorCode != SqlLiteCommandStore.SqlliteDuplicateKeyError) throw sqliteException;
                    _log.WarnFormat(
                        "MsSqlMessageStore: A duplicate Command with the CommandId {0} was inserted into the Message Store, ignoring and continuing",
                        command.Id);
                }
            }
        }

        public T Get<T>(Guid id, int timeoutInMilliseconds = -1) where T : class, IRequest, new()
        {
            var sql = string.Format("select * from {0} where CommandId = @CommandId", this.MessageStoreTableName);
            var parameters = new[]
            {
                this.CreateSqlParameter("CommandId", id)
            };

            return ExecuteCommand(command => ReadCommand<T>(command.ExecuteReader()), sql, timeoutInMilliseconds, this, parameters);
        }

        public async Task AddAsync<T>(T command, int timeoutInMilliseconds = -1, CancellationToken? ct = null) where T : class, IRequest
        {
            var parameters = InitAddDbParameters(command, this);

            using (var connection = this.GetConnection())
            {
                await connection.OpenAsync(ct ?? CancellationToken.None).ConfigureAwait(this.ContinueOnCapturedContext);
                var sqlcmd = InitAddDbCommand(timeoutInMilliseconds, connection, parameters, this);
                try
                {
                    await
                        sqlcmd.ExecuteNonQueryAsync(ct ?? CancellationToken.None)
                            .ConfigureAwait(this.ContinueOnCapturedContext);
                }
                catch (SqliteException sqliteException)
                {
                    if (sqliteException.SqliteErrorCode != SqlliteDuplicateKeyError) throw sqliteException;
                    _log.WarnFormat(
                        "MsSqlMessageStore: A duplicate Command with the CommandId {0} was inserted into the Message Store, ignoring and continuing",
                        command.Id);
                } 
                
            }
        }

        public async Task<T> GetAsync<T>(Guid id, int timeoutInMilliseconds = -1, CancellationToken? ct = null) where T : class, IRequest, new()
        {
            var sql = string.Format("select * from {0} where CommandId = @CommandId", this.MessageStoreTableName);
            var parameters = new[]
            {
                this.CreateSqlParameter("@CommandId", id)
            };
            var result = await ExecuteCommandAsync(
                        async command => ReadCommand<T>(
                            await
                                command.ExecuteReaderAsync(ct ?? CancellationToken.None)
                                    .ConfigureAwait(this.ContinueOnCapturedContext)),
                        sql,
                        timeoutInMilliseconds, this, parameters, ct)
                    .ConfigureAwait(this.ContinueOnCapturedContext);
            return result;
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

        public ILog Log
        {
            get { return _log; }
        }
        
        public SqlLiteCommandStoreConfiguration Configuration
        {
            get { return _configuration; }
        }

        public string MessageStoreTableName
        {
            get { return Configuration.MessageStoreTableName; }
        }

        public DbParameter CreateSqlParameter(string parameterName, object value)
        {
            return new SqliteParameter(parameterName, value);
        }

        public DbConnection GetConnection()
        {
            return new SqliteConnection(_configuration.ConnectionString);
        }


        public T ExecuteCommand<T>(Func<DbCommand, T> execute, string sql, int timeoutInMilliseconds,
            SqlLiteCommandStore sqlLiteCommandStore, params DbParameter[] parameters)
        {
            using (var connection = sqlLiteCommandStore.GetConnection())
            using (var command = connection.CreateCommand())
            {
                if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                connection.Open();
                var item = execute(command);
                return item;
            }
        }

        public async Task<T> ExecuteCommandAsync<T>(Func<DbCommand, Task<T>> execute, string sql, int timeoutInMilliseconds,
            SqlLiteCommandStore sqlLiteCommandStore, DbParameter[] parameters, CancellationToken? ct = null)
        {
            using (var connection = sqlLiteCommandStore.GetConnection())
            using (var command = connection.CreateCommand())
            {
                if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                await connection.OpenAsync(ct ?? CancellationToken.None).ConfigureAwait(sqlLiteCommandStore.ContinueOnCapturedContext);
                var item = await execute(command).ConfigureAwait(sqlLiteCommandStore.ContinueOnCapturedContext);
                return item;
            }
        }

        public DbCommand InitAddDbCommand(int timeoutInMilliseconds, DbConnection connection, DbParameter[] parameters, SqlLiteCommandStore sqlLiteCommandStore)
        {
            var sqlAdd = string.Format(
                "insert into {0} (CommandID, CommandType, CommandBody, Timestamp) values (@CommandID, @CommandType, @CommandBody, @Timestamp)", sqlLiteCommandStore.MessageStoreTableName);

            var sqlcmd = connection.CreateCommand();
            if (timeoutInMilliseconds != -1) sqlcmd.CommandTimeout = timeoutInMilliseconds;

            sqlcmd.CommandText = sqlAdd;
            AddParamtersParamArrayToCollection(parameters, sqlcmd);
            return sqlcmd;
        }

        public DbParameter[] InitAddDbParameters<T>(T command, SqlLiteCommandStore sqlLiteCommandStore) where T : class, IRequest
        {
            var commandJson = JsonConvert.SerializeObject(command);
            var parameters = new[]
            {
                sqlLiteCommandStore.CreateSqlParameter("CommandID", command.Id), //was CommandId
                sqlLiteCommandStore.CreateSqlParameter("CommandType", typeof (T).Name), sqlLiteCommandStore.CreateSqlParameter("CommandBody", commandJson), sqlLiteCommandStore.CreateSqlParameter("Timestamp", DateTime.UtcNow)
            };
            return parameters;
        }

        public TResult ReadCommand<TResult>(IDataReader dr) where TResult : class, IRequest, new()
        {
            if (dr.Read())
            {
                var body = dr.GetString(dr.GetOrdinal("CommandBody"));
                return JsonConvert.DeserializeObject<TResult>(body);
            }

            return new TResult { Id = Guid.Empty };
        }

        public void AddParamtersParamArrayToCollection(DbParameter[] parameters, DbCommand command)
        {
            //command.Parameters.AddRange(parameters); used to work... but can't with current sqllite lib. Iterator issue
            for (var index = 0; index < parameters.Length; index++)
            {
                command.Parameters.Add(parameters[index]);
            }
        }
    }
}