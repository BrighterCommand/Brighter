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
using Npgsql;
using NpgsqlTypes;
using Paramore.Brighter.Logging;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Outbox.PostgreSql
{
    public class
        PostgreSqlOutbox : RelationDatabaseOutbox<NpgsqlConnection, NpgsqlCommand, NpgsqlDataReader, NpgsqlParameter>
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgreSqlOutbox>();

        private readonly PostgreSqlConfiguration _configuration;
        private readonly IPostgreSqlConnectionProvider _connectionProvider;


        public PostgreSqlOutbox(
            PostgreSqlConfiguration configuration,
            IPostgreSqlConnectionProvider connectionProvider) : base(
            configuration.OutBoxTableName, new PostgreSqlQueries(), ApplicationLogging.CreateLogger<PostgreSqlOutbox>())
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
        }

        public PostgreSqlOutbox(PostgreSqlConfiguration configuration)
            : this(configuration, new PostgreSqlNpgsqlConnectionProvider(configuration))
        { }

        protected override void WriteToStore(IAmABoxTransactionConnectionProvider transactionConnectionProvider,
            Func<NpgsqlConnection, NpgsqlCommand> commandFunc,
            Action loggingAction)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null &&
                transactionConnectionProvider is IPostgreSqlConnectionProvider provider)
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
                catch (PostgresException sqlException)
                {
                    if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
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
            Func<NpgsqlConnection, NpgsqlCommand> commandFunc,
            Action loggingAction,
            CancellationToken cancellationToken)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null &&
                transactionConnectionProvider is IPostgreSqlConnectionProvider provider)
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
                catch (PostgresException sqlException)
                {
                    if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
                    {
                        s_logger.LogWarning(
                            "PostgresSqlOutbox: A duplicate was detected in the batch");
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

        protected override T ReadFromStore<T>(Func<NpgsqlConnection, NpgsqlCommand> commandFunc,
            Func<NpgsqlDataReader, T> resultFunc)
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

        protected override async Task<T> ReadFromStoreAsync<T>(Func<NpgsqlConnection, NpgsqlCommand> commandFunc,
            Func<NpgsqlDataReader, Task<T>> resultFunc, CancellationToken cancellationToken)
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

        protected override NpgsqlCommand CreateCommand(
            NpgsqlConnection connection,
            string sqlText,
            int outBoxTimeout,
            params NpgsqlParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected override NpgsqlParameter[] CreatePagedOutstandingParameters(double milliSecondsSinceAdded,
            int pageSize, int pageNumber)
        {
            var offset = (pageNumber - 1) * pageSize;
            var parameters = new NpgsqlParameter[3];
            parameters[0] = CreateSqlParameter("OffsetValue", offset);
            parameters[1] = CreateSqlParameter("PageSize", pageSize);
            parameters[2] = CreateSqlParameter("OutstandingSince", milliSecondsSinceAdded);

            return parameters;
        }

        protected override NpgsqlParameter CreateSqlParameter(string parameterName, object value)
        {
            return new NpgsqlParameter { ParameterName = parameterName, Value = value };
        }

        protected override NpgsqlParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagjson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new[]
            {
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}MessageId", NpgsqlDbType = NpgsqlDbType.Uuid, Value = message.Id
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}MessageType",
                    NpgsqlDbType = NpgsqlDbType.Text,
                    Value = message.Header.MessageType.ToString()
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}Topic",
                    NpgsqlDbType = NpgsqlDbType.Text,
                    Value = message.Header.Topic
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}Timestamp",
                    NpgsqlDbType = NpgsqlDbType.TimestampTz,
                    Value = message.Header.TimeStamp
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}CorrelationId",
                    NpgsqlDbType = NpgsqlDbType.Uuid,
                    Value = message.Header.CorrelationId
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}ReplyTo",
                    NpgsqlDbType = NpgsqlDbType.Varchar,
                    Value = message.Header.ReplyTo
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}ContentType",
                    NpgsqlDbType = NpgsqlDbType.Varchar,
                    Value = message.Header.ContentType
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}PartitionKey",
                    NpgsqlDbType = NpgsqlDbType.Varchar,
                    Value = message.Header.PartitionKey
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}HeaderBag", NpgsqlDbType = NpgsqlDbType.Text, Value = bagjson
                },
                _configuration.BinaryMessagePayload ? new NpgsqlParameter
                {
                    ParameterName = $"{prefix}Body",
                    NpgsqlDbType = NpgsqlDbType.Bytea,
                    Value = message.Body.Bytes
                } 
                : new NpgsqlParameter
                {
                    ParameterName = $"{prefix}Body",
                    NpgsqlDbType = NpgsqlDbType.Text,
                    Value = message.Body.Value
                }
            };
        }

        protected override Message MapFunction(NpgsqlDataReader dr)
        {
            if (dr.Read())
            {
                return MapAMessage(dr);
            }

            return new Message();
        }

        protected override async Task<Message> MapFunctionAsync(NpgsqlDataReader dr,
            CancellationToken cancellationToken)
        {
            if (await dr.ReadAsync(cancellationToken))
            {
                return MapAMessage(dr);
            }

            return new Message();
        }

        protected override IEnumerable<Message> MapListFunction(NpgsqlDataReader dr)
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
            NpgsqlDataReader dr,
            CancellationToken cancellationToken)
        {
            var messages = new List<Message>();
            while (await dr.ReadAsync(cancellationToken))
            {
                messages.Add(MapAMessage(dr));
            }

            await dr.CloseAsync();

            return messages;
        }

        public Message MapAMessage(IDataReader dr)
        {
            var id = GetMessageId(dr);
            var messageType = GetMessageType(dr);
            var topic = GetTopic(dr);

            DateTime timeStamp = GetTimeStamp(dr);
            var correlationId = GetCorrelationId(dr);
            var replyTo = GetReplyTo(dr);
            var contentType = GetContentType(dr);
            var partitionKey = GetPartitionKey(dr);

            var header = new MessageHeader(
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

            var body = _configuration.BinaryMessagePayload
                ? new MessageBody(((NpgsqlDataReader)dr).GetFieldValue<byte[]>(dr.GetOrdinal("Body")))
                :new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

            return new Message(header, body);
        }

        private string GetContentType(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal))
                return null;

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
            if (dr.IsDBNull(ordinal))
                return null;

            var correlationId = dr.GetGuid(ordinal);
            return correlationId;
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
            return dr.GetGuid(dr.GetOrdinal("MessageId"));
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
            if (dr.IsDBNull(ordinal))
                return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
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
