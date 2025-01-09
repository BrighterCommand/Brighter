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
    /// <summary>
    /// Implements an outbox using PostgreSQL as a backing store
    /// </summary>
    public class PostgreSqlOutbox : RelationDatabaseOutbox
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgreSqlOutbox>();

        private readonly IAmARelationalDatabaseConfiguration _configuration;
        private readonly IAmARelationalDbConnectionProvider _connectionProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostgreSqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        /// <param name="connectionProvider">Provides a connection to the Db that allows us to enlist in an ambient transaction</param>
        public PostgreSqlOutbox(
            IAmARelationalDatabaseConfiguration configuration,
            IAmARelationalDbConnectionProvider connectionProvider) : base(
            configuration.OutBoxTableName, new PostgreSqlQueries(), ApplicationLogging.CreateLogger<PostgreSqlOutbox>())
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PostgreSqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        /// <param name="dataSource">From v7.0 Npgsql uses an Npgsql data source, leave null to have Brighter manage
        /// connections; Brighter will not manage type mapping for you in this case so you must register them
        /// globally</param>
        public PostgreSqlOutbox(
            IAmARelationalDatabaseConfiguration configuration,
            NpgsqlDataSource dataSource = null)
            : this(configuration, new PostgreSqlConnectionProvider(configuration, dataSource))
        {
        }

        protected override void WriteToStore(
            IAmABoxTransactionProvider<DbTransaction> transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action loggingAction)
        {
            var connection = GetOpenConnection(_connectionProvider, transactionProvider);
            using var command = commandFunc.Invoke(connection);
            try
            {
                if (transactionProvider is { HasOpenTransaction: true })
                    command.Transaction = transactionProvider.GetTransaction();
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
                FinishWrite(connection, transactionProvider);
            }
        }

        protected override async Task WriteToStoreAsync(
            IAmABoxTransactionProvider<DbTransaction> transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action loggingAction,
            CancellationToken cancellationToken)
        {
            var connection = await GetOpenConnectionAsync(_connectionProvider, transactionProvider, cancellationToken);
            await using var command = commandFunc.Invoke(connection);
            try
            {
                if (transactionProvider is { HasOpenTransaction: true })
                    command.Transaction = await transactionProvider.GetTransactionAsync(cancellationToken);
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
                FinishWrite(connection, transactionProvider);
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
            using var command = commandFunc.Invoke(connection);
            try
            {
                return resultFunc.Invoke(command.ExecuteReader());
            }
            finally
            {
                connection.Close();
            }
        }

        protected override async Task<T> ReadFromStoreAsync<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, Task<T>> resultFunc,
            CancellationToken cancellationToken
        )
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
            using var command = commandFunc.Invoke(connection);
            try
            {
                return await resultFunc.Invoke(await command.ExecuteReaderAsync(cancellationToken));
            }
            finally
            {
                connection.Close();
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

        protected override IDbDataParameter[] CreatePagedOutstandingParameters(TimeSpan since, int pageSize,
            int pageNumber)
        {
            var parameters = new IDbDataParameter[3];
            parameters[0] = CreateSqlParameter("TimestampSince", DateTimeOffset.UtcNow.Subtract(since));
            parameters[1] = CreateSqlParameter("Take", pageSize);
            parameters[2] = CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize);

            return parameters;
        }

        protected override IDbDataParameter[] CreatePagedDispatchedParameters(TimeSpan dispatchedSince, int pageSize,
            int pageNumber)
        {
            var parameters = new IDbDataParameter[3];
            parameters[0] = CreateSqlParameter("DispatchedSince", DateTimeOffset.UtcNow.Subtract(dispatchedSince));
            parameters[1] = CreateSqlParameter("Take", pageSize);
            parameters[2] = CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize);

            return parameters;
        }

        protected override IDbDataParameter[] CreatePagedReadParameters(int pageSize, int pageNumber)
        {
            var parameters = new IDbDataParameter[2];
            parameters[0] = CreateSqlParameter("Take", pageSize);
            parameters[1] = CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize);

            return parameters;
        }

        protected override IDbDataParameter CreateSqlParameter(string parameterName, object value)
        {
            return new NpgsqlParameter { ParameterName = parameterName, Value = value };
        }

        protected override IDbDataParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagjson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new[]
            {
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}MessageId", NpgsqlDbType = NpgsqlDbType.Text, Value = message.Id
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
                    Value = message.Header.Topic.Value
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}Timestamp",
                    NpgsqlDbType = NpgsqlDbType.TimestampTz,
                    Value = message.Header.TimeStamp.ToUniversalTime()
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}CorrelationId",
                    NpgsqlDbType = NpgsqlDbType.Text,
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
                _configuration.BinaryMessagePayload
                    ? new NpgsqlParameter
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
            CancellationToken cancellationToken
        )
        {
            var messages = new List<Message>();
            while (await dr.ReadAsync(cancellationToken))
            {
                messages.Add(MapAMessage(dr));
            }

            dr.Close();

            return messages;
        }

        protected override async Task<int> MapOutstandingCountAsync(DbDataReader dr,
            CancellationToken cancellationToken)
        {
            int outstandingMessages = -1;
            if (await dr.ReadAsync(cancellationToken))
            {
                outstandingMessages = dr.GetInt32(0);
            }

            await dr.CloseAsync();

            return outstandingMessages;
        }

        protected override int MapOutstandingCount(DbDataReader dr)
        {
            int outstandingMessages = -1;
            if (dr.Read())
            {
                outstandingMessages = dr.GetInt32(0);
            }

            dr.Close();

            return outstandingMessages;
        }

        private Message MapAMessage(DbDataReader dr)
        {
            var id = GetMessageId(dr);
            var messageType = GetMessageType(dr);
            var topic = GetTopic(dr);

            DateTimeOffset timeStamp = GetTimeStamp(dr);
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
                delayed: TimeSpan.Zero,
                correlationId: correlationId,
                replyTo: new RoutingKey(replyTo),
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
                : new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

            return new Message(header, body);
        }

        private string GetContentType(DbDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal))
                return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private static Dictionary<string, object> GetContextBag(DbDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            var dictionaryBag =
                JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        private string GetCorrelationId(DbDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal))
                return null;

            var correlationId = dr.GetString(ordinal);
            return correlationId;
        }

        private static RoutingKey GetTopic(DbDataReader dr)
        {
            return new RoutingKey(dr.GetString(dr.GetOrdinal("Topic")));
        }

        private static MessageType GetMessageType(DbDataReader dr)
        {
            return (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
        }

        private static string GetMessageId(DbDataReader dr)
        {
            return dr.GetString(dr.GetOrdinal("MessageId"));
        }

        private string GetPartitionKey(DbDataReader dr)
        {
            var ordinal = dr.GetOrdinal("PartitionKey");
            if (dr.IsDBNull(ordinal)) return null;

            var partitionKey = dr.GetString(ordinal);
            return partitionKey;
        }

        private string GetReplyTo(DbDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ReplyTo");
            if (dr.IsDBNull(ordinal))
                return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private static DateTimeOffset GetTimeStamp(DbDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Timestamp");
            var timeStamp = dr.IsDBNull(ordinal)
                ? DateTimeOffset.MinValue
                : dr.GetDateTime(ordinal);
            return timeStamp;
        }
    }
}
