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
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MySql;

namespace Paramore.Brighter.Outbox.MySql
{
    /// <summary>
    /// Implements an outbox using Sqlite as a backing store  
    /// </summary>
    public class MySqlOutbox : RelationDatabaseOutbox
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MySqlOutbox>();

        private const int MySqlDuplicateKeyError = 1062;
        private readonly IAmARelationalDatabaseConfiguration _configuration;
        private readonly IAmARelationalDbConnectionProvider  _connectionProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        /// <param name="connectionProvider">Provides a connection to the Db that allows us to enlist in an ambient transaction</param>
        public MySqlOutbox(IAmARelationalDatabaseConfiguration configuration, IAmARelationalDbConnectionProvider connectionProvider) 
            : base(configuration.OutBoxTableName, new MySqlQueries(), ApplicationLogging.CreateLogger<MySqlOutbox>())
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
            ContinueOnCapturedContext = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        public MySqlOutbox(IAmARelationalDatabaseConfiguration configuration) 
            : this(configuration, new MySqlConnectionProvider(configuration))
        {
        }

        protected override void WriteToStore(
            IAmABoxTransactionProvider<DbTransaction> transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action loggingAction
            )
        {
            var connectionProvider = _connectionProvider;
            if (transactionProvider is IAmARelationalDbConnectionProvider transConnectionProvider)
                connectionProvider = transConnectionProvider;

            var connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                connection.Open();
            using (var command = commandFunc.Invoke(connection))
            {
                try
                {
                    if (transactionProvider != null && transactionProvider.HasOpenTransaction)
                        command.Transaction = transactionProvider.GetTransaction();
                    command.ExecuteNonQuery();
                }
                catch (MySqlException sqlException)
                {
                    if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                    {
                        s_logger.LogWarning(
                            "MsSqlOutbox: A duplicate was detected in the batch");
                        return;
                    }

                    throw;
                }
                finally
                {
                    transactionProvider?.Close();
                }
            }
        }

        protected override async Task WriteToStoreAsync(
            IAmABoxTransactionProvider<DbTransaction> transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action loggingAction, 
            CancellationToken cancellationToken
            )
        {
            var connectionProvider = _connectionProvider;
            if (transactionProvider is IAmARelationalDbConnectionProvider transConnectionProvider)
                connectionProvider = transConnectionProvider;

            var connection = await connectionProvider.GetConnectionAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
            using (var command = commandFunc.Invoke(connection))
            {
                try
                {
                    if (transactionProvider != null && transactionProvider.HasOpenTransaction)
                        command.Transaction = await transactionProvider.GetTransactionAsync(cancellationToken);
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (MySqlException sqlException)
                {
                    if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                    {
                        s_logger.LogWarning(
                            "MsSqlOutbox: A duplicate was detected in the batch");
                        return;
                    }

                    throw;
                }
                finally
                {
                    transactionProvider?.Close();
                }
            }
        }

        protected override T ReadFromStore<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, T> resultFunc
            )
        {
            var connection = _connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                connection.Open();
            using (var command = commandFunc.Invoke(connection))
            {
                try
                {
                    return resultFunc.Invoke(command.ExecuteReader());
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        protected override async Task<T> ReadFromStoreAsync<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, Task<T>> resultFunc, 
            CancellationToken cancellationToken)
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
            using (var command = commandFunc.Invoke(connection))
            {
                try
                {
                    return await resultFunc.Invoke(await command.ExecuteReaderAsync(cancellationToken));
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        protected override DbCommand CreateCommand(
            DbConnection connection, 
            string sqlText, 
            int outBoxTimeout,
            params IDbDataParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected override IDbDataParameter CreateSqlParameter(string parameterName, object value)
        {
            return new MySqlParameter { ParameterName = parameterName, Value = value };
        }


        protected override IDbDataParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new[]
            {
                new MySqlParameter
                {
                    ParameterName = $"@{prefix}MessageId", DbType = DbType.String, Value = message.Id
                },
                new MySqlParameter
                {
                    ParameterName = $"@{prefix}MessageType",
                    DbType = DbType.String,
                    Value = message.Header.MessageType.ToString()
                },
                new MySqlParameter
                {
                    ParameterName = $"@{prefix}Topic", DbType = DbType.String, Value = message.Header.Topic,
                },
                new MySqlParameter
                {
                    ParameterName = $"@{prefix}Timestamp",
                    DbType = DbType.DateTime2,
                    Value = message.Header.TimeStamp.ToUniversalTime()
                }, //always store in UTC, as this is how we query messages
                new MySqlParameter
                {
                    ParameterName = $"@{prefix}CorrelationId",
                    DbType = DbType.String,
                    Value = message.Header.CorrelationId
                },
                new MySqlParameter
                {
                    ParameterName = $"@{prefix}ReplyTo", DbType = DbType.String, Value = message.Header.ReplyTo
                },
                new MySqlParameter
                {
                    ParameterName = $"@{prefix}ContentType",
                    DbType = DbType.String,
                    Value = message.Header.ContentType
                },
                new MySqlParameter
                {
                    ParameterName = $"@{prefix}PartitionKey",
                    DbType = DbType.String,
                    Value = message.Header.PartitionKey
                },
                new MySqlParameter { ParameterName = $"@{prefix}HeaderBag", DbType = DbType.String, Value = bagJson },
                _configuration.BinaryMessagePayload
                    ? new MySqlParameter
                    {
                        ParameterName = $"@{prefix}Body", DbType = DbType.Binary, Value = message.Body.Bytes
                    }
                    : new MySqlParameter
                    {
                        ParameterName = $"@{prefix}Body", DbType = DbType.String, Value = message.Body.Value
                    }
            };
        }

        protected override IDbDataParameter[] CreatePagedOutstandingParameters(
            double milliSecondsSinceAdded,
            int pageSize, 
            int pageNumber
            )
        {
            var offset = (pageNumber - 1) * pageSize;
            var parameters = new IDbDataParameter[3];
            parameters[0] = CreateSqlParameter("OffsetValue", offset);
            parameters[1] = CreateSqlParameter("PageSize", pageSize);
            parameters[2] = CreateSqlParameter("OutstandingSince", milliSecondsSinceAdded);

            return parameters;
        }

        protected override Message MapFunction(DbDataReader dr)
        {
            if (dr.Read())
            {
                return MapAMessage(dr);
            }

            return new Message();
        }

        protected override async Task<Message> MapFunctionAsync(DbDataReader dr, CancellationToken cancellationToken)
        {
            if (await dr.ReadAsync(cancellationToken))
            {
                return MapAMessage(dr);
            }

            return new Message();
        }

        protected override IEnumerable<Message> MapListFunction(DbDataReader dr)
        {
            var messages = new List<Message>();
            while (dr.Read())
            {
                messages.Add(MapAMessage(dr));
            }

            dr.Close();

            return messages;
        }

        protected override async Task<IEnumerable<Message>> MapListFunctionAsync(
            DbDataReader dr,
            CancellationToken cancellationToken)
        {
            var messages = new List<Message>();
            while (await dr.ReadAsync(cancellationToken))
            {
                messages.Add(MapAMessage(dr));
            }

            dr.Close();

            return messages;
        }


        protected override async Task<int> MapOutstandingCountAsync(DbDataReader dr, CancellationToken cancellationToken)
        {
            int outstandingMessages = -1;
            if (await dr.ReadAsync(cancellationToken))
            {
                outstandingMessages = dr.GetInt32(0);
            }
            dr.Close();
            return outstandingMessages;
        }

        private static bool IsExceptionUnqiueOrDuplicateIssue(MySqlException sqlException)
        {
            return sqlException.Number == MySqlDuplicateKeyError;
        }

        private Message MapAMessage(IDataReader dr)
        {
            var id = GetMessageId(dr);
            var messageType = GetMessageType(dr);
            var topic = GetTopic(dr);

            var header = new MessageHeader(id, topic, messageType);

            if (dr.FieldCount > 4)
            {
                DateTime timeStamp = GetTimeStamp(dr);
                var correlationId = GetCorrelationId(dr);
                var replyTo = GetReplyTo(dr);
                var contentType = GetContentType(dr);
                var partitionKey = GetPartitionKey(dr);

                header = new MessageHeader(
                    messageId: id,
                    topic: topic,
                    messageType: messageType,
                    timeStamp: timeStamp,
                    handledCount: 0,
                    delayedMilliseconds: 0,
                    correlationId: correlationId,
                    replyTo: replyTo,
                    contentType: contentType,
                    partitionKey: partitionKey);

                Dictionary<string, object> dictionaryBag = GetContextBag(dr);
                if (dictionaryBag != null)
                {
                    foreach (var key in dictionaryBag.Keys)
                    {
                        header.Bag.Add(key, dictionaryBag[key]);
                    }
                }
            }

            var body = _configuration.BinaryMessagePayload
                ? new MessageBody(GetBodyAsBytes((MySqlDataReader)dr), "application/octet-stream", CharacterEncoding.Raw)
                : new MessageBody(GetBodyAsString(dr), "application/json", CharacterEncoding.UTF8);

            return new Message(header, body);
        }

       private byte[] GetBodyAsBytes(MySqlDataReader dr)
        {
            var i = dr.GetOrdinal("Body");
            using (var ms = new MemoryStream())
            {
                var buffer = new byte[1024];
                int offset = 0;
                var bytesRead = dr.GetBytes(i, offset, buffer, 0, 1024);
                while (bytesRead > 0)
                {
                    ms.Write(buffer, offset, (int)bytesRead);
                    offset += (int)bytesRead;
                    bytesRead = dr.GetBytes(i, offset, buffer, 0, 1024);
                }

                ms.Flush();
                var body = ms.ToArray();
                return body;
            }
        }

        private static string GetBodyAsString(IDataReader dr)
        {
            return dr.GetString(dr.GetOrdinal("Body"));
        }

         private static Dictionary<string, object> GetContextBag(IDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            var dictionaryBag =
                JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        private string GetContentType(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal)) return null;

            var contentType = dr.GetString(ordinal);
            return contentType;
        }

        private string GetCorrelationId(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal)) return null;

            var correlationId = dr.GetString(ordinal);
            return correlationId;
        }

        private static MessageType GetMessageType(IDataReader dr)
        {
            return (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
        }

        private static string GetMessageId(IDataReader dr)
        {
            return dr.GetString(dr.GetOrdinal("MessageId"));
        }

        private string GetPartitionKey(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("PartitionKey");
            if (dr.IsDBNull(ordinal)) return null;

            var partitionKey = dr.GetString(ordinal);
            return partitionKey;
        }

        private string GetReplyTo(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ReplyTo");
            if (dr.IsDBNull(ordinal)) return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private static string GetTopic(IDataReader dr)
        {
            return dr.GetString(dr.GetOrdinal("Topic"));
        }

        private static DateTime GetTimeStamp(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Timestamp");
            var timeStamp = dr.IsDBNull(ordinal)
                ? DateTime.MinValue
                : dr.GetDateTime(ordinal);
            return timeStamp;
        }
    }
}
