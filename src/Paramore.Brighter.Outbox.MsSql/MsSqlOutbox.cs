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
using System.Linq;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.Outbox.MsSql
{
    /// <summary>
    /// Class MsSqlOutbox.
    /// </summary>
    public class MsSqlOutbox :
        RelationDatabaseOutbox<SqlConnection, SqlCommand, SqlDataReader, SqlParameter>
    {
        private const int MsSqlDuplicateKeyError_UniqueIndexViolation = 2601;
        private const int MsSqlDuplicateKeyError_UniqueConstraintViolation = 2627;
        private readonly MsSqlConfiguration _configuration;
        private readonly IMsSqlConnectionProvider _connectionProvider;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="connectionProvider">The connection factory.</param>
        public MsSqlOutbox(MsSqlConfiguration configuration, IMsSqlConnectionProvider connectionProvider) : base(
            configuration.OutBoxTableName, new MsSqlQueries(), ApplicationLogging.CreateLogger<MsSqlOutbox>())
        {
            _configuration = configuration;
            ContinueOnCapturedContext = false;
            _connectionProvider = connectionProvider;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public MsSqlOutbox(MsSqlConfiguration configuration) : this(configuration,
            new MsSqlSqlAuthConnectionProvider(configuration))
        {
        }

        protected override void WriteToStore(IAmABoxTransactionConnectionProvider transactionConnectionProvider,
            Func<SqlConnection, SqlCommand> commandFunc, Action loggingAction)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null &&
                transactionConnectionProvider is IMsSqlTransactionConnectionProvider provider)
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
                catch (SqlException sqlException)
                {
                    if (sqlException.Number == MsSqlDuplicateKeyError_UniqueIndexViolation ||
                        sqlException.Number == MsSqlDuplicateKeyError_UniqueConstraintViolation)
                    {
                        loggingAction.Invoke();
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
            Func<SqlConnection, SqlCommand> commandFunc, Action loggingAction, CancellationToken cancellationToken)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null &&
                transactionConnectionProvider is IMsSqlTransactionConnectionProvider provider)
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
                catch (SqlException sqlException)
                {
                    if (sqlException.Number == MsSqlDuplicateKeyError_UniqueIndexViolation ||
                        sqlException.Number == MsSqlDuplicateKeyError_UniqueConstraintViolation)
                    {
                        loggingAction.Invoke();
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

        protected override T ReadFromStore<T>(Func<SqlConnection, SqlCommand> commandFunc,
            Func<SqlDataReader, T> resultFunc)
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

        protected override async Task<T> ReadFromStoreAsync<T>(Func<SqlConnection, SqlCommand> commandFunc,
            Func<SqlDataReader, Task<T>> resultFunc, CancellationToken cancellationToken)
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

        protected override SqlCommand CreateCommand(SqlConnection connection, string sqlText, int outBoxTimeout,
            params SqlParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected override SqlParameter[] CreatePagedOutstandingParameters(double milliSecondsSinceAdded, int pageSize,
            int pageNumber)
        {
            var parameters = new SqlParameter[3];
            parameters[0] =
                new SqlParameter { ParameterName = "PageNumber", Value = (object)pageNumber ?? DBNull.Value };
            parameters[1] = new SqlParameter { ParameterName = "PageSize", Value = (object)pageSize ?? DBNull.Value };
            parameters[2] = new SqlParameter
            {
                ParameterName = "OutstandingSince", Value = (object)milliSecondsSinceAdded ?? DBNull.Value
            };

            return parameters;
        }

        #region Parameter Helpers

        protected override SqlParameter CreateSqlParameter(string parameterName, object value)
        {
            return new SqlParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };
        }

        protected override SqlParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new[]
            {
                new SqlParameter
                {
                    ParameterName = $"{prefix}MessageId", 
                    DbType = DbType.Guid,
                    Value = (object)message.Id ?? DBNull.Value
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}MessageType",
                    DbType = DbType.String,
                    Value = (object)message.Header.MessageType.ToString() ?? DBNull.Value
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}Topic", 
                    DbType = DbType.String,
                    Value = (object)message.Header.Topic ?? DBNull.Value
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}Timestamp",
                    DbType = DbType.DateTime,
                    Value = (object)message.Header.TimeStamp.ToUniversalTime() ?? DBNull.Value
                }, //always store in UTC, as this is how we query messages
                new SqlParameter
                {
                    ParameterName = $"{prefix}CorrelationId",
                    DbType = DbType.Guid,
                    Value = (object)message.Header.CorrelationId ?? DBNull.Value
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}ReplyTo", 
                    DbType = DbType.String,
                    Value = (object)message.Header.ReplyTo ?? DBNull.Value
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}ContentType",
                    DbType = DbType.String,
                    Value = (object)message.Header.ContentType ?? DBNull.Value
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}PartitionKey",
                    DbType = DbType.String,
                    Value = (object)message.Header.PartitionKey ?? DBNull.Value
                },
                new SqlParameter { ParameterName = $"{prefix}HeaderBag", 
                    Value = (object)bagJson ?? DBNull.Value },
                _configuration.BinaryMessagePayload
                    ? new SqlParameter
                    {
                        ParameterName = $"{prefix}Body", 
                        DbType = DbType.Binary,
                        Value = (object)message.Body?.Bytes ?? DBNull.Value
                    }
                    : new SqlParameter
                    {
                        ParameterName = $"{prefix}Body", 
                        DbType = DbType.String,
                        Value = (object)message.Body?.Value ?? DBNull.Value
                    }
            };
        }

        #endregion

        #region Property Extractors

        private static string GetTopic(SqlDataReader dr) => dr.GetString(dr.GetOrdinal("Topic"));

        private static MessageType GetMessageType(SqlDataReader dr) =>
            (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));

        private static Guid GetMessageId(SqlDataReader dr) => dr.GetGuid(dr.GetOrdinal("MessageId"));

        private string GetContentType(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal)) return null;

            var contentType = dr.GetString(ordinal);
            return contentType;
        }

        private string GetReplyTo(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ReplyTo");
            if (dr.IsDBNull(ordinal)) return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private static Dictionary<string, object> GetContextBag(SqlDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            var dictionaryBag =
                JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        private Guid? GetCorrelationId(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal)) return null;

            var correlationId = dr.GetGuid(ordinal);
            return correlationId;
        }

        private static DateTime GetTimeStamp(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Timestamp");
            var timeStamp = dr.IsDBNull(ordinal)
                ? DateTime.MinValue
                : dr.GetDateTime(ordinal);
            return timeStamp;
        }

        private string GetPartitionKey(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("PartitionKey");
            if (dr.IsDBNull(ordinal)) return null;

            var partitionKey = dr.GetString(ordinal);
            return partitionKey;
        }

        private byte[] GetBodyAsBytes(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Body");
            if (dr.IsDBNull(ordinal)) return null;
            
            var body = dr.GetStream(ordinal);
            long bodyLength = body.Length;
            var buffer = new byte[bodyLength];
            body.Read(buffer, 0, (int)bodyLength);
            return buffer;
        }
        
        private static string GetBodyAsText(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Body");
            return dr.IsDBNull(ordinal) ? null : dr.GetString(ordinal);
        }

        #endregion

        #region DataReader Operators

        protected override Message MapFunction(SqlDataReader dr)
        {
            Message message = null;
            if (dr.Read())
            {
                message = MapAMessage(dr);
            }

            dr.Close();

            return message ?? new Message();
        }

        protected override async Task<Message> MapFunctionAsync(SqlDataReader dr, CancellationToken cancellationToken)
        {
            Message message = null;
            if (await dr.ReadAsync(cancellationToken))
            {
                message = MapAMessage(dr);
            }

            dr.Close();

            return message ?? new Message();
        }

        protected override IEnumerable<Message> MapListFunction(SqlDataReader dr)
        {
            var messages = new List<Message>();
            while (dr.Read())
            {
                messages.Add(MapAMessage(dr));
            }

            dr.Close();

            return messages;
        }

        protected override async Task<IEnumerable<Message>> MapListFunctionAsync(SqlDataReader dr,
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

        #endregion

        private Message MapAMessage(SqlDataReader dr)
        {
            var id = GetMessageId(dr);
            var messageType = GetMessageType(dr);
            var topic = GetTopic(dr);

            var header = new MessageHeader(id, topic, messageType);

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

            var bodyOrdinal = dr.GetOrdinal("Body");
            string messageBody = string.Empty;
            var body = _configuration.BinaryMessagePayload
                ? new MessageBody(GetBodyAsBytes((SqlDataReader)dr), "application/octet-stream", CharacterEncoding.Raw)
                : new MessageBody(GetBodyAsText(dr), "application/json", CharacterEncoding.UTF8);
            return new Message(header, body);
        }
    }
}
