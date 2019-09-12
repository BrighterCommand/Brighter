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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Outbox.MySql
{
    /// <summary>
    ///     Class MySqlOutbox.
    /// </summary>
    public class MySqlOutbox :
        IAmAnOutbox<Message>,
        IAmAnOutboxAsync<Message>,
        IAmAnOutboxViewer<Message>,
        IAmAnOutboxViewerAsync<Message>
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<MySqlOutbox>);

        private const int MySqlDuplicateKeyError = 1062;
        private readonly MySqlOutboxConfiguration _configuration;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MySqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>


        /// <summary>
        ///     If false we the default thread synchronization context to run any continuation, if true we re-use the original
        ///     synchronization context.
        ///     Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        ///     or access the Result or otherwise block. You may need the orginating synchronization context if you need to access
        ///     thread specific storage
        ///     such as HTTPContext
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }
        
        public MySqlOutbox(MySqlOutboxConfiguration configuration)
        {
            _configuration = configuration;
            ContinueOnCapturedContext = false;
        }

        /// <summary>
        ///     Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="outBoxTimeout"></param>
        /// <returns>Task.</returns>
        public void Add(Message message, int outBoxTimeout = -1)
        {
            var parameters = InitAddDbParameters(message);

            using (var connection = GetConnection())
            {
                connection.Open();
                var sql = GetAddSql();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddParamtersParamArrayToCollection(parameters.ToArray(), command);

                    try
                    {
                        command.ExecuteNonQuery();
                    }
                    catch (MySqlException sqlException)
                    {
                        if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                        {
                            _logger.Value.WarnFormat("MsSqlOutbox: A duplicate Message with the MessageId {0} was inserted into the Outbox, ignoring and continuing",
                                message.Id);
                            return;
                        }

                        throw;
                    }
                }
            }
        }

        public async Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var parameters = InitAddDbParameters(message);

            using (var connection = GetConnection())
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                using (var command = connection.CreateCommand())
                {
                    var sql = GetAddSql();
                    command.CommandText = sql;
                    AddParamtersParamArrayToCollection(parameters.ToArray(), command);

                    try
                    {
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                    }
                    catch (MySqlException sqlException)
                    {
                        if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                        {
                            _logger.Value.WarnFormat("MsSqlOutbox: A duplicate Message with the MessageId {0} was inserted into the Outbox, ignoring and continuing",
                                message.Id);
                            return;
                        }

                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Which messages have been dispatched from the Outbox, within the specified time window
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago can the message have been dispatched</param>
        /// <param name="pageSize">How many messages per page</param>
        /// <param name="pageNumber">Which page number to return</param>
        /// <param name="outboxTimeout">When do we give up?</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>Messages that have been dispatched from the Outbox to the broker</returns>
        public IEnumerable<Message> DispatchedMessages(
            double millisecondsDispatchedSince, 
            int pageSize = 100, 
            int pageNumber = 1,
            int outboxTimeout = -1, 
            Dictionary<string, object> args = null)
       {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                CreatePagedDispatchedCommand(command, millisecondsDispatchedSince, pageSize, pageNumber);

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
       /// <summary>
       /// Get a message
       /// </summary>
       /// <param name="messageId">The id of the message to retrieve</param>
       /// <param name="outBoxTimeout">Timeout in milleseconds</param>
       /// <returns></returns>
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            var sql = string.Format("SELECT * FROM {0} WHERE MessageId = @MessageId", _configuration.OutBoxTableName);
            var parameters = new[]
            {
                CreateSqlParameter("@MessageId", messageId.ToString())
            };

            return ExecuteCommand(command => MapFunction(command.ExecuteReader()), sql, outBoxTimeout, parameters);
        }

       /// <summary>
        /// get as an asynchronous operation.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task{Message}" />.</returns>
        public async Task<Message> GetAsync(
           Guid messageId, 
           int outBoxTimeout = -1, 
           CancellationToken cancellationToken = default(CancellationToken))
        {
            var sql = string.Format("SELECT * FROM {0} WHERE MessageId = @MessageId", _configuration.OutBoxTableName);
            var parameters = new[]
            {
                CreateSqlParameter("@MessageId", messageId.ToString())
            };

            return await ExecuteCommandAsync(
                async command => MapFunction(await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext)),
                sql,
                outBoxTimeout,
                cancellationToken,
                parameters)
                .ConfigureAwait(ContinueOnCapturedContext);
        }


        /// <summary>
        /// Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns></returns>
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1, Dictionary<string, object> args = null)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                CreatePagedReadCommand(command, pageSize, pageNumber);

                connection.Open();

                using (var dbDataReader = command.ExecuteReader())
                {
                    var messages = new List<Message>();
                    while (dbDataReader.Read())
                    {
                        messages.Add(MapAMessage(dbDataReader));
                    }
                    return messages;
                }
            }
        }

        /// <summary>
        /// Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="args">Additional parameters required for search, if any</param>
         /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns></returns>
       public async Task<IList<Message>> GetAsync(
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                CreatePagedReadCommand(command, pageSize, pageNumber);

                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

                var messages = new List<Message>();
                using (var dbDataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                {
                    while (await dbDataReader.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                    {
                        messages.Add(MapAMessage(dbDataReader));
                    }
                }

                return messages;
            }
        }
        
         /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, CancellationToken cancellationToken = default(CancellationToken))
        {
           using (var connection = GetConnection())
           {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                using (var command = InitMarkDispatchedCommand(connection, id, dispatchedAt))
                {
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                }
           }
        }
         
        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null)
        {
           using (var connection = GetConnection())
           {
                connection.Open();
                using (var command = InitMarkDispatchedCommand(connection, id, dispatchedAt))
                {
                    command.ExecuteNonQuery();
                }
           }
        }
          
        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="millSecondsSinceSent">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>Outstanding Messages</returns>
       public IEnumerable<Message> OutstandingMessages(
            double millSecondsSinceSent, 
            int pageSize = 100, 
            int pageNumber = 1,
            Dictionary<string, object> args = null)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                CreatePagedOutstandingCommand(command, millSecondsSinceSent, pageSize, pageNumber);

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

        private MySqlParameter CreateSqlParameter(string parameterName, object value)
        {
            return new MySqlParameter
            {
                ParameterName = parameterName,
                Value = value
            };
        }
        
        private void CreatePagedDispatchedCommand(DbCommand command, double millisecondsDispatchedSince, int pageSize, int pageNumber)
        {
            var pagingSqlFormat = "SELECT * FROM {0} AS TBL WHERE `CreatedID` BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) AND DISPATCHED IS NOT NULL AND DISPATCHED < DATEADD(millisecond, @OutStandingSince, getdate()) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
            var parameters = new[]
            {
                CreateSqlParameter("@PageNumber", pageNumber),
                CreateSqlParameter("@PageSize", pageSize),
                CreateSqlParameter("@OutstandingSince", -1 * millisecondsDispatchedSince)
            };

            var sql = string.Format(pagingSqlFormat, _configuration.OutBoxTableName);

            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }
                
        private void CreatePagedReadCommand(DbCommand command, int pageSize, int pageNumber)
        {
            var parameters = new[]
            {
                CreateSqlParameter("@PageNumber", pageNumber),
                CreateSqlParameter("@PageSize", pageSize)
            };

            var sql = string.Format("SELECT * FROM {0} AS TBL WHERE `CreatedID` BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC", _configuration.OutBoxTableName);

            command.CommandText = sql;
            AddParamtersParamArrayToCollection(parameters, command);
        }
         
        private void CreatePagedOutstandingCommand(DbCommand command, double milliSecondsSinceAdded, int pageSize, int pageNumber)
        {
            var pagingSqlFormat = "SELECT * FROM {0} AS TBL WHERE `CreatedID` BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) AND DISPATCHED IS NULL AND TIMESTAMP < DATEADD(millisecond, @OutStandingSince, getdate()) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
            var parameters = new[]
            {
                CreateSqlParameter("@PageNumber", pageNumber),
                CreateSqlParameter("@PageSize", pageSize),
                CreateSqlParameter("@OutstandingSince", milliSecondsSinceAdded)
            };

            var sql = string.Format(pagingSqlFormat, _configuration.OutBoxTableName);

            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }

                
        private T ExecuteCommand<T>(Func<DbCommand, T> execute, string sql, int outboxTimeout, params MySqlParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                if (outboxTimeout != -1) command.CommandTimeout = outboxTimeout;

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
            params MySqlParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                var item = await execute(command).ConfigureAwait(ContinueOnCapturedContext);
                return item;
            }
        }

        private string GetAddSql()
        {
            var sql =
                string.Format(
                    "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @HeaderBag, @Body)",
                    _configuration.OutBoxTableName);
            return sql;
        }

        private MySqlConnection GetConnection()
        {
            return new MySqlConnection(_configuration.ConnectionString);
        }

        private MySqlParameter[] InitAddDbParameters(Message message)
        {
            var bagJson = JsonConvert.SerializeObject(message.Header.Bag);
            return new[]
            {
                new MySqlParameter
                {
                    ParameterName = "@MessageId",
                    DbType = DbType.String,
                    Value = message.Id.ToString()
                },
                new MySqlParameter
                {
                    ParameterName = "@MessageType",
                    DbType = DbType.String,
                    Value = message.Header.MessageType.ToString()
                },
                new MySqlParameter
                {
                    ParameterName = "@Topic",
                    DbType = DbType.String,
                    Value = message.Header.Topic,
                },
                new MySqlParameter
                {
                    ParameterName = "@Timestamp",
                    DbType = DbType.DateTime2,
                    Value = message.Header.TimeStamp
                },
                new MySqlParameter
                {
                    ParameterName = "@HeaderBag",
                    DbType = DbType.String,
                    Value = bagJson
                },
                new MySqlParameter
                {
                    ParameterName = "@Body",
                    DbType = DbType.String,
                    Value = message.Body.Value
                }
            };
        }

        private DbCommand InitMarkDispatchedCommand(DbConnection connection, Guid messageId, DateTime? dispatchedAt)
        {
            var command = connection.CreateCommand();
            var sql = $"UPDATE {_configuration.OutBoxTableName} SET Dispatched = @DispatchedAt WHERE MessageId = @MessageId";
            command.CommandText = sql;
            command.Parameters.Add(CreateSqlParameter("@MessageId", messageId));
            command.Parameters.Add(CreateSqlParameter("@DispatchedAt", dispatchedAt));
            return command;
        }
        
        private static bool IsExceptionUnqiueOrDuplicateIssue(MySqlException sqlException)
        {
            return sqlException.Number == MySqlDuplicateKeyError;
        }

        private Message MapAMessage(IDataReader dr)
        {
            var id = dr.GetGuid(0);
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
                var dictionaryBag = JsonConvert.DeserializeObject<Dictionary<string, string>>(headerBag);
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


        public void AddParamtersParamArrayToCollection(MySqlParameter[] parameters, DbCommand command)
        {
            command.Parameters.AddRange(parameters);
        }

    }
}
