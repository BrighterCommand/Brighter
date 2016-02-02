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
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messagestore.mssql
{
    /// <summary>
    ///     Class MsSqlMessageStore.
    /// </summary>
    public class MsSqlMessageStore : IAmAMessageStore<Message>, IAmAnAsyncMessageStore<Message>,
        IAmAMessageStoreViewer<Message>
    {
        private const int MsSqlDuplicateKeyError = 2601;
        private const int SqlCeDuplicateKeyError = 25016;
        private readonly MsSqlMessageStoreConfiguration _configuration;
        private readonly JavaScriptSerializer _javaScriptSerializer;
        private readonly ILog _log;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlMessageStore" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public MsSqlMessageStore(MsSqlMessageStoreConfiguration configuration)
            : this(configuration, LogProvider.GetCurrentClassLogger()) {}

        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlMessageStore" /> class.
        ///     Use this constructor if you need to pass in the logger
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="log">The log.</param>
        public MsSqlMessageStore(MsSqlMessageStoreConfiguration configuration, ILog log)
        {
            _configuration = configuration;
            _log = log;
            _javaScriptSerializer = new JavaScriptSerializer();
            ContinueOnCapturedContext = false;
        }

        /// <summary>
        ///     Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public void Add(Message message, int messageStoreTimeout = -1)
        {
            var parameters = InitAddDbParameters(message);

            using (var connection = GetConnection())
            {
                connection.Open();
                var command = InitAddDbCommand(connection, parameters);

                try
                {
                    command.ExecuteNonQuery();
                }
                catch (SqlException sqlException)
                {
                    if (sqlException.Number == MsSqlDuplicateKeyError)
                    {
                        _log.WarnFormat(
                            "MsSqlMessageStore: A duplicate Message with the MessageId {0} was inserted into the Message Store, ignoring and continuing",
                            message.Id);
                        return;
                    }

                    throw;
                }
                catch (SqlCeException sqlCeException)
                {
                    if (sqlCeException.NativeError == SqlCeDuplicateKeyError)
                    {
                        _log.WarnFormat(
                            "MsSqlMessageStore: A duplicate Message with the MessageId {0} was inserted into the Message Store, ignoring and continuing",
                            message.Id);
                        return;
                    }

                    throw;
                }
            }
        }

        private DbCommand InitAddDbCommand(DbConnection connection, DbParameter[] parameters)
        {
            var command = connection.CreateCommand();
            var sql =
                string.Format(
                    "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @HeaderBag, @Body)",
                    _configuration.MessageStoreTableName);
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
            return command;
        }

        private DbParameter[] InitAddDbParameters(Message message)
        {
            var bagJson = _javaScriptSerializer.Serialize(message.Header.Bag);
            var parameters = new[]
            {
                CreateSqlParameter("MessageId", message.Id),
                CreateSqlParameter("MessageType", message.Header.MessageType.ToString()),
                CreateSqlParameter("Topic", message.Header.Topic),
                CreateSqlParameter("Timestamp", message.Header.TimeStamp),
                CreateSqlParameter("HeaderBag", bagJson),
                CreateSqlParameter("Body", message.Body.Value)
            };
            return parameters;
        }

        /// <summary>
        ///     Gets the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <returns>Task&lt;Message&gt;.</returns>
        public Message Get(Guid messageId, int messageStoreTimeout = -1)
        {
            var sql = string.Format("SELECT * FROM {0} WHERE MessageId = @MessageId",
                _configuration.MessageStoreTableName);
            var parameters = new[]
            {
                CreateSqlParameter("MessageId", messageId)
            };

            return ExecuteCommand(command => MapFunction(command.ExecuteReader()), sql, messageStoreTimeout, parameters);
        }

        /// <summary>
        ///     Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <returns></returns>
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                SetPagingCommandFor(command, _configuration, pageSize, pageNumber);

                connection.Open();

                var dbDataReader = command.ExecuteReader();

                var messages = new List<Message>();
                while (dbDataReader.Read())
                {
                    messages.Add(MapAMessage(dbDataReader));
                }
                return messages;
            }
        }

        public async Task AddAsync(Message message, int messageStoreTimeout = -1, CancellationToken? ct = null)
        {
            var parameters = InitAddDbParameters(message);

            using (var connection = GetConnection())
            {
                await connection.OpenAsync(ct ?? CancellationToken.None).ConfigureAwait(ContinueOnCapturedContext);
                var command = InitAddDbCommand(connection, parameters);

                try
                {
                    await command.ExecuteNonQueryAsync(ct ?? CancellationToken.None).ConfigureAwait(ContinueOnCapturedContext);
                }
                catch (SqlException sqlException)
                {
                    if (sqlException.Number == MsSqlDuplicateKeyError)
                    {
                        _log.WarnFormat(
                            "MsSqlMessageStore: A duplicate Message with the MessageId {0} was inserted into the Message Store, ignoring and continuing",
                            message.Id);
                        return;
                    }

                    throw;
                }
                catch (SqlCeException sqlCeException)
                {
                    if (sqlCeException.NativeError == SqlCeDuplicateKeyError)
                    {
                        _log.WarnFormat(
                            "MsSqlMessageStore: A duplicate Message with the MessageId {0} was inserted into the Message Store, ignoring and continuing",
                            message.Id);
                        return;
                    }

                    throw;
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

        public async Task<Message> GetAsync(Guid messageId, int messageStoreTimeout = -1, CancellationToken? ct = null)
        {
            var sql = string.Format("SELECT * FROM {0} WHERE MessageId = @MessageId",
                _configuration.MessageStoreTableName);
            var parameters = new[]
            {
                CreateSqlParameter("MessageId", messageId)
            };

            var result =
                await
                    ExecuteCommandAsync(
                        async command =>
                            MapFunction(
                                await
                                    command.ExecuteReaderAsync(ct ?? CancellationToken.None)
                                        .ConfigureAwait(ContinueOnCapturedContext)),
                        sql,
                        messageStoreTimeout,
                        ct,
                        parameters
                        )
                        .ConfigureAwait(ContinueOnCapturedContext);
            return result;
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

        private T ExecuteCommand<T>(Func<DbCommand, T> execute, string sql, int messageStoreTimeout,
            params DbParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddRange(parameters);

                if (messageStoreTimeout != -1) command.CommandTimeout = messageStoreTimeout;

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
            switch (_configuration.Type)
            {
                case MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer:
                    return new SqlConnection(_configuration.ConnectionString);
                case MsSqlMessageStoreConfiguration.DatabaseType.SqlCe:
                    return new SqlCeConnection(_configuration.ConnectionString);
            }
            return null;
        }

        private Message MapAMessage(IDataReader dr)
        {
            var id = dr.GetGuid(dr.GetOrdinal("MessageId"));
            var messageType = (MessageType) Enum.Parse(typeof (MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
            var topic = dr.GetString(dr.GetOrdinal("Topic"));

            var header = new MessageHeader(id, topic, messageType);

            if (dr.FieldCount > 4)
            {
                //new schema....we've got the extra header information
                var ordinal = dr.GetOrdinal("Timestamp");
                var timeStamp = dr.IsDBNull(ordinal)
                    ? DateTime.MinValue
                    : dr.GetDateTime(ordinal);
                header = new MessageHeader(id, topic, messageType, timeStamp, 0, 0);

                var i = dr.GetOrdinal("HeaderBag");
                var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
                var dictionaryBag = _javaScriptSerializer.Deserialize<Dictionary<string, string>>(headerBag);
                if (dictionaryBag != null)
                {
                    foreach (var key in dictionaryBag.Keys)
                    {
                        header.Bag.Add(key, dictionaryBag[key]);
                    }
                }
            }

            var body = new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

            return new Message(header, body);
        }

        private Message MapFunction(IDataReader dr)
        {
            if (dr.Read())
            {
                return MapAMessage(dr);
            }

            return new Message();
        }

        private void SetPagingCommandFor(DbCommand command, MsSqlMessageStoreConfiguration configuration, int pageSize,
            int pageNumber)
        {
            string pagingSqlFormat;
            DbParameter[] parameters;
            switch (configuration.Type)
            {
                case MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer:
                    //works 2005+
                    pagingSqlFormat =
                        "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
                    parameters = new[]
                    {
                        CreateSqlParameter("PageNumber", pageNumber)
                        , CreateSqlParameter("PageSize", pageSize)
                    };
                    break;
                case MsSqlMessageStoreConfiguration.DatabaseType.SqlCe:
                    //2012+/ce only
                    pagingSqlFormat =
                        "SELECT * FROM {0} ORDER BY Timestamp DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                    parameters = new[]
                    {
                        CreateSqlParameter("Offset", (pageNumber - 1)*pageSize)
                        //sqlce doesn't like arithmetic in offset...
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