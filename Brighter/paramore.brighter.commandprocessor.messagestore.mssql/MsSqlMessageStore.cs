// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messagestore.mssql
// Author           : ian
// Created          : 01-26-2015
//
// Last Modified By : ian
// Last Modified On : 02-26-2015
// ***********************************************************************
// <copyright file="MsSqlMessageStore.cs" company="">
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
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SqlServerCe;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messagestore.mssql
{
    /// <summary>
    /// Class MsSqlMessageStore.
    /// </summary>
    public class MsSqlMessageStore : IAmAMessageStore<Message>, IAmAMessageStoreViewer<Message>
    {
        private readonly MsSqlMessageStoreConfiguration _configuration;
        private readonly ILog _log;
        private readonly JavaScriptSerializer _javaScriptSerializer;
        private const int MsSqlDuplicateKeyError = 2601;
        private const int SqlCeDuplicateKeyError = 25016;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlMessageStore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="log">The log.</param>
        public MsSqlMessageStore(MsSqlMessageStoreConfiguration configuration, ILog log)
        {
            _configuration = configuration;
            _log = log;
            _javaScriptSerializer = new JavaScriptSerializer();
        }

        /// <summary>
        /// Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public async Task Add(Message message)
        {
            var sql = string.Format("INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @HeaderBag, @Body)", _configuration.MessageStoreTableName);
            var bagJson = _javaScriptSerializer.Serialize(message.Header.Bag);
            var parameters = new[]
            {
                CreateSqlParameter("MessageId", message.Id),
                CreateSqlParameter("MessageType", message.Header.MessageType.ToString()),
                CreateSqlParameter("Topic", message.Header.Topic),
                CreateSqlParameter("Timestamp", message.Header.TimeStamp),
                CreateSqlParameter("HeaderBag", bagJson),
                CreateSqlParameter("Body", message.Body.Value),
            };

            using (var connection = GetConnection())
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();

                command.CommandText = sql;
                command.Parameters.AddRange(parameters);
                try
                {
                    await command.ExecuteNonQueryAsync();
                }
                catch (SqlException sqlException)
                {
                    if (sqlException.Number == MsSqlDuplicateKeyError)
                    {
                        _log.WarnFormat("MsSqlMessageStore: A duplicate Message with the MessageId {0} was inserted into the Message Store, ignoring and continuing", message.Id);
                        return;
                    }

                    throw;
                }
                catch (SqlCeException sqlCeException)
                {
                    if (sqlCeException.NativeError == SqlCeDuplicateKeyError)
                    {
                        _log.WarnFormat("MsSqlMessageStore: A duplicate Message with the MessageId {0} was inserted into the Message Store, ignoring and continuing", message.Id);
                        return;
                    }

                    throw;
                }
            }
        }

        private DbParameter CreateSqlParameter(string parameterName, object value)
        {
            switch (_configuration.Type)
            {
                case MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer:
                    return new SqlParameter(parameterName, value);
                case MsSqlMessageStoreConfiguration.DatabaseType.SqlCe:
                    return new SqlCeParameter(parameterName, value);
            }
            return null;
        }

        private DbConnection GetConnection()
        {
            switch (_configuration.Type)
            {
                case MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer:
                    return new SqlConnection(_configuration.ConnectionString);
                case MsSqlMessageStoreConfiguration.DatabaseType.SqlCe:
                    return new SqlCeConnection(_configuration.ConnectionString);
            }
            return null;
        }

        /// <summary>
        /// Gets the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <returns>Task&lt;Message&gt;.</returns>
        public async Task<Message> Get(Guid messageId)
        {
            var sql = string.Format("SELECT * FROM {0} WHERE MessageId = @MessageId", _configuration.MessageStoreTableName);
            var parameters = new[]
            {
                CreateSqlParameter("MessageId", messageId)
            };

            var result = await ExecuteCommand(async command => MapFunction(await command.ExecuteReaderAsync()), sql, parameters);
            return result;
        }

        private Message MapFunction(IDataReader dr)
        {
            if (dr.Read())
            {
                return MapAMessage(dr);
            }

            return new Message();
        }

        private Message MapAMessage(IDataReader dr)
        {
            var id = dr.GetGuid(dr.GetOrdinal("MessageId"));
            var messageType = (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
            var topic = dr.GetString(dr.GetOrdinal("Topic"));

            var header = new MessageHeader(id, topic, messageType);

            if (dr.FieldCount > 4)
            {
                //new schema....we've got the extra header information
                var ordinal = dr.GetOrdinal("Timestamp");
                var timeStamp = dr.IsDBNull(ordinal)
                    ? DateTime.MinValue
                    : dr.GetDateTime(ordinal);
                header = new MessageHeader(id, topic, messageType, timeStamp, 0);

                var i = dr.GetOrdinal("HeaderBag");
                var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
                var dictionaryBag = _javaScriptSerializer.Deserialize<Dictionary<string, string>>(headerBag);

                foreach (var key in dictionaryBag.Keys)
                {
                    header.Bag.Add(key, dictionaryBag[key]);
                }
            }

            var body = new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

            return new Message(header, body);
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

        /// <summary>
        /// Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <returns></returns>
        public async Task<IList<Message>> Get(int pageSize = 100, int pageNumber = 1)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                SetPagingCommandFor(command, _configuration, pageSize, pageNumber);
                
                await connection.OpenAsync();

                var dbDataReader = await command.ExecuteReaderAsync();

                var messages = new List<Message>();
                while (dbDataReader.Read())
                {
                    messages.Add(MapAMessage(dbDataReader));
                }
                return messages;
            }
        }

        private void SetPagingCommandFor(DbCommand command, MsSqlMessageStoreConfiguration configuration, int pageSize, int pageNumber)
        {
            string pagingSqlFormat;
            DbParameter[] parameters;
            switch (configuration.Type)
            {
                case MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer:
                    //works 2005+
                    pagingSqlFormat=
                        "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
                    parameters = new[]
                    {
                        CreateSqlParameter("PageNumber", pageNumber)
                        , CreateSqlParameter("PageSize", pageSize)
                    };
                    break;
                case MsSqlMessageStoreConfiguration.DatabaseType.SqlCe:
                    //2012+/ce only
                    pagingSqlFormat="SELECT * FROM {0} ORDER BY Timestamp DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                    parameters = new[]
                    {
                        
                        CreateSqlParameter("Offset", (pageNumber-1) * pageSize) //sqlce doesn't like arithmetic in offset...
                        , CreateSqlParameter("PageSize", pageSize)
                    };
                    break;
                default:
                    throw new ArgumentException("Cannot generate command for sql env " + configuration.Type);
            }
            
            var sql = string.Format(pagingSqlFormat, _configuration.MessageStoreTableName);
            
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }
    }
}