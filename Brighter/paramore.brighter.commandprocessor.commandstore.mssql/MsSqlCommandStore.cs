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
using System.Data.SqlClient;
using System.Data.SqlServerCe;
using System.Threading.Tasks;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.commandstore.mssql
{
    /// <summary>
    /// Class MsSqlCommandStore.
    /// </summary>
    public class MsSqlCommandStore : IAmACommandStore
    {
        private readonly MsSqlCommandStoreConfiguration _configuration;
        private readonly ILog _log;
        private const int MsSqlDuplicateKeyError = 2601;
        private const int SqlCeDuplicateKeyError = 25016;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="log">The log.</param>
        public MsSqlCommandStore(MsSqlCommandStoreConfiguration configuration, ILog log)
        {
            _configuration = configuration;
            _log = log;
        }

        /// <summary>
        /// Adds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="command">The command.</param>
        /// <returns>Task.</returns>
        public async Task Add<T>(Guid id, T command) where T : class, IRequest
        {
            var sql = string.Format("insert into {0} (CommandID, CommandType, Topic,CommandBody, Timestamp) values (@CommandID, @CommandType, @Topic,CommandBody, @Timestamp)", _configuration.MessageStoreTableName);
            var commandJson = JsonConvert.SerializeObject(command);
            var parameters = new[]
            {
                CreateSqlParameter("CommandId", command.Id),
                CreateSqlParameter("CommandType", typeof(T).Name),
                CreateSqlParameter("CommandBody", commandJson),
                CreateSqlParameter("Timestamp", DateTime.UtcNow)
            };

            using (var connection = GetConnection())
            {
                await connection.OpenAsync();
                var sqlcmd = connection.CreateCommand();

                sqlcmd .CommandText = sql;
                sqlcmd .Parameters.AddRange(parameters);
                try
                {
                    await sqlcmd .ExecuteNonQueryAsync();
                }
                catch (SqlException sqlException)
                {
                    if (sqlException.Number == MsSqlDuplicateKeyError)
                    {
                        _log.WarnFormat("MsSqlMessageStore: A duplicate Message with the MessageId {0} was inserted into the Message Store, ignoring and continuing", command.Id);
                        return;
                    }

                    throw;
                }
                catch (SqlCeException sqlCeException)
                {
                    if (sqlCeException.NativeError == SqlCeDuplicateKeyError)
                    {
                        _log.WarnFormat("MsSqlMessageStore: A duplicate Message with the MessageId {0} was inserted into the Message Store, ignoring and continuing", command.Id);
                        return;
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Finds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <returns>T.</returns>
        public async Task<T> Get<T>(Guid id) where T : class, IRequest, new()
        {
            var sql = string.Format("select * from {0} where CommandId = @commandId", _configuration.MessageStoreTableName);
            var parameters = new[]
            {
                CreateSqlParameter("CommandId", id)
            };

            var result = await ExecuteCommand(async command => ReadCommand<T>(await command.ExecuteReaderAsync()), sql, parameters);
            return result;
        }

        private async Task<T> ExecuteCommand<T>(Func<DbCommand, Task<T>> execute, string sql, params DbParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddRange(parameters);

                await connection.OpenAsync();
                T item = await execute(command);
                return item;
            }
        }

        private DbParameter CreateSqlParameter(string parameterName, object value)
        {
            switch (_configuration.Type)
            {
                case MsSqlCommandStoreConfiguration.DatabaseType.MsSqlServer:
                    return new SqlParameter(parameterName, value);
                case MsSqlCommandStoreConfiguration.DatabaseType.SqlCe:
                    return new SqlCeParameter(parameterName, value);
            }
            return null;
        }

        private DbConnection GetConnection()
        {
            switch (_configuration.Type)
            {
                case MsSqlCommandStoreConfiguration.DatabaseType.MsSqlServer:
                    return new SqlConnection(_configuration.ConnectionString);
                case MsSqlCommandStoreConfiguration.DatabaseType.SqlCe:
                    return new SqlCeConnection(_configuration.ConnectionString);
            }
            return null;
        }

        private TResult ReadCommand<TResult>(IDataReader dr) where TResult : class, new()
        {
            if (dr.Read())
            {
                var body = dr.GetString(dr.GetOrdinal("CommandBody"));
                return JsonConvert.DeserializeObject<TResult>(body);
            }

            return new TResult();
        }

    }
}
