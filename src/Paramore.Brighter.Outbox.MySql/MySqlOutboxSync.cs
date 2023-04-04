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
    ///     Class MySqlOutbox.
    /// </summary>
    public class
        MySqlOutbox : RelationDatabaseOutbox<MySqlConnection, MySqlCommand, MySqlDataReader, MySqlParameter>
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MySqlOutbox>();

        private const int MySqlDuplicateKeyError = 1062;
        private readonly MySqlConfiguration _configuration;
        private readonly IMySqlConnectionProvider _connectionProvider;

        public MySqlOutbox(MySqlConfiguration configuration, IMySqlConnectionProvider connectionProvider) : base(
            configuration.OutBoxTableName, new MySqlQueries(), ApplicationLogging.CreateLogger<MySqlOutbox>())
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
            ContinueOnCapturedContext = false;
        }

        public MySqlOutbox(MySqlConfiguration configuration) : this(configuration,
            new MySqlConnectionProvider(configuration))
        {
        }

        protected override void WriteToStore(IAmABoxTransactionConnectionProvider transactionConnectionProvider,
            Func<MySqlConnection, MySqlCommand> commandFunc,
            Action loggingAction)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null &&
                transactionConnectionProvider is IMySqlTransactionConnectionProvider provider)
                connectionProvider = provider;

            var connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                connection.Open();
            using (var command = commandFunc.Invoke(connection))
            {
                try
                {
                    if (transactionConnectionProvider != null && connectionProvider.HasOpenTransaction)
                        command.Transaction = connectionProvider.GetTransaction();
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
                    if (!connectionProvider.IsSharedConnection)
                        connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction)
                        connection.Close();
                }
            }
        }

        protected override async Task WriteToStoreAsync(
            IAmABoxTransactionConnectionProvider transactionConnectionProvider,
            Func<MySqlConnection, MySqlCommand> commandFunc,
            Action loggingAction, CancellationToken cancellationToken)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null &&
                transactionConnectionProvider is IMySqlTransactionConnectionProvider provider)
                connectionProvider = provider;

            var connection = await connectionProvider.GetConnectionAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
            using (var command = commandFunc.Invoke(connection))
            {
                try
                {
                    if (transactionConnectionProvider != null && connectionProvider.HasOpenTransaction)
                        command.Transaction = connectionProvider.GetTransaction();
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
                    if (!connectionProvider.IsSharedConnection)
                        connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction)
                        await connection.CloseAsync();
                }
            }
        }

        protected override T ReadFromStore<T>(Func<MySqlConnection, MySqlCommand> commandFunc,
            Func<MySqlDataReader, T> resultFunc)
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
                    if (!_connectionProvider.IsSharedConnection)
                        connection.Dispose();
                    else if (!_connectionProvider.HasOpenTransaction)
                        connection.Close();
                }
            }
        }

        protected override async Task<T> ReadFromStoreAsync<T>(Func<MySqlConnection, MySqlCommand> commandFunc,
            Func<MySqlDataReader, Task<T>> resultFunc, CancellationToken cancellationToken)
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
                    if (!_connectionProvider.IsSharedConnection)
                        connection.Dispose();
                    else if (!_connectionProvider.HasOpenTransaction)
                        connection.Close();
                }
            }
        }

        protected override MySqlCommand CreateCommand(MySqlConnection connection, string sqlText, int outBoxTimeout,
            params MySqlParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected override MySqlParameter CreateSqlParameter(string parameterName, object value)
        {
            return new MySqlParameter { ParameterName = parameterName, Value = value };
        }


        protected override MySqlParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new[]
            {
                new MySqlParameter
                {
                    ParameterName = $"@{prefix}MessageId", DbType = DbType.String, Value = message.Id.ToString()
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
                    Value = message.Header.CorrelationId.ToString()
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

        protected override MySqlParameter[] CreatePagedOutstandingParameters(double milliSecondsSinceAdded,
            int pageSize, int pageNumber)
        {
            var offset = (pageNumber - 1) * pageSize;
            var parameters = new MySqlParameter[3];
            parameters[0] = CreateSqlParameter("OffsetValue", offset);
            parameters[1] = CreateSqlParameter("PageSize", pageSize);
            parameters[2] = CreateSqlParameter("OutstandingSince", milliSecondsSinceAdded);

            return parameters;
        }

        protected override Message MapFunction(MySqlDataReader dr)
        {
            if (dr.Read())
            {
                return MapAMessage(dr);
            }

            return new Message();
        }

        protected override async Task<Message> MapFunctionAsync(MySqlDataReader dr, CancellationToken cancellationToken)
        {
            if (await dr.ReadAsync(cancellationToken))
            {
                return MapAMessage(dr);
            }

            return new Message();
        }

        protected override IEnumerable<Message> MapListFunction(MySqlDataReader dr)
        {
            var messages = new List<Message>();
            while (dr.Read())
            {
                messages.Add(MapAMessage(dr));
            }

            dr.Close();

            return messages;
        }

        protected override async Task<IEnumerable<Message>> MapListFunctionAsync(MySqlDataReader dr,
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

                header = new MessageHeader(
                    messageId: id,
                    topic: topic,
                    messageType: messageType,
                    timeStamp: timeStamp,
                    handledCount: 0,
                    delayedMilliseconds: 0,
                    correlationId: correlationId,
                    replyTo: replyTo,
                    contentType: contentType);

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
                ? new MessageBody(GetBodyAsBytes((MySqlDataReader)dr))
                : new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

            return new Message(header, body);
        }

        private byte[] GetBodyAsBytes(MySqlDataReader dr)
        {
            var i = dr.GetOrdinal("Body");
            var body = dr.GetStream(i);
            long bodyLength = body.Length;
            var buffer = new byte[bodyLength];
            body.Read(buffer,0, (int)bodyLength);
            return buffer;
        }

        private static string GetTopic(IDataReader dr)
        {
            return dr.GetString(dr.GetOrdinal("Topic"));
        }

        private static MessageType GetMessageType(IDataReader dr)
        {
            return (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
        }

        private static Guid GetMessageId(IDataReader dr)
        {
            return dr.GetGuid(0);
        }

        private string GetContentType(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal)) return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private string GetReplyTo(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ReplyTo");
            if (dr.IsDBNull(ordinal)) return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private static Dictionary<string, object> GetContextBag(IDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            var dictionaryBag =
                JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        private Guid? GetCorrelationId(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal)) return null;

            var correlationId = dr.GetGuid(ordinal);
            return correlationId;
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
