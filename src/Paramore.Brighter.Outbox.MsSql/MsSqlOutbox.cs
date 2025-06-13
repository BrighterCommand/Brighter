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
using System.IO;
using System.Net.Mime;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.MsSql
{
    /// <summary>
    /// Implements an Outbox using MSSQL as a backing store 
    /// </summary>
    public class MsSqlOutbox : RelationDatabaseOutbox
    {
        private const int MsSqlDuplicateKeyError_UniqueIndexViolation = 2601;
        private const int MsSqlDuplicateKeyError_UniqueConstraintViolation = 2627;
        private readonly IAmARelationalDatabaseConfiguration _configuration;
        private readonly IAmARelationalDbConnectionProvider _connectionProvider;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="connectionProvider">The connection factory.</param>
        public MsSqlOutbox(IAmARelationalDatabaseConfiguration configuration,
            IAmARelationalDbConnectionProvider connectionProvider) : base(DbSystem.MySql, configuration.DatabaseName,
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
        public MsSqlOutbox(IAmARelationalDatabaseConfiguration configuration) : this(configuration,
            new MsSqlConnectionProvider(configuration))
        {
        }

        protected override void WriteToStore(
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction)
        {
            var connection = GetOpenConnection(_connectionProvider, transactionProvider);
            using var command = commandFunc.Invoke(connection);
            try
            {
                if (transactionProvider is { HasOpenTransaction: true })
                    command.Transaction = transactionProvider.GetTransaction();
                command.ExecuteNonQuery();
            }
            catch (SqlException sqlException)
            {
                if (sqlException.Number != MsSqlDuplicateKeyError_UniqueIndexViolation &&
                    sqlException.Number != MsSqlDuplicateKeyError_UniqueConstraintViolation) throw;
                loggingAction?.Invoke();
            }
            finally
            {
                FinishWrite(connection, transactionProvider);
            }
        }

        protected override async Task WriteToStoreAsync(
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction,
            CancellationToken cancellationToken)
        {
            var connection = await GetOpenConnectionAsync(_connectionProvider, transactionProvider, cancellationToken);
            using var command = commandFunc.Invoke(connection);
            try
            {
                if (transactionProvider is { HasOpenTransaction: true })
                    command.Transaction = await transactionProvider.GetTransactionAsync(cancellationToken);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException sqlException)
            {
                if (sqlException.Number == MsSqlDuplicateKeyError_UniqueIndexViolation ||
                    sqlException.Number == MsSqlDuplicateKeyError_UniqueConstraintViolation)
                {
                    loggingAction?.Invoke();
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
            params IDbDataParameter[] parameters
        )
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
            parameters[0] = new SqlParameter { ParameterName = "PageNumber", Value = pageNumber };
            parameters[1] = new SqlParameter { ParameterName = "PageSize", Value = pageSize };
            parameters[2] = CreateSqlParameter("DispatchedSince", DateTimeOffset.UtcNow.Subtract(since));
            return parameters;
        }

        protected override IDbDataParameter[] CreatePagedDispatchedParameters(TimeSpan dispatchedSince, int pageSize,
            int pageNumber)
        {
            var parameters = new IDbDataParameter[3];
            parameters[0] = new SqlParameter { ParameterName = "PageNumber", Value = pageNumber };
            parameters[1] = new SqlParameter { ParameterName = "PageSize", Value = pageSize };
            parameters[2] = CreateSqlParameter("DispatchedSince", DateTimeOffset.UtcNow.Subtract(dispatchedSince));

            return parameters;
        }

        protected override IDbDataParameter[] CreatePagedReadParameters(int pageSize, int pageNumber)
        {
            var parameters = new IDbDataParameter[2];
            parameters[0] = new SqlParameter { ParameterName = "PageNumber", Value = pageNumber };
            parameters[1] = new SqlParameter { ParameterName = "PageSize", Value = pageSize };
            return parameters;
        }

        #region Parameter Helpers

        protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
        {
            return new SqlParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };
        }

        protected override IDbDataParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return
            [
                new SqlParameter
                {
                    ParameterName = $"{prefix}MessageId",
                    DbType = DbType.String,
                    Value = message.Id
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}MessageType",
                    DbType = DbType.String,
                    Value = message.Header.MessageType.ToString()
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}Topic",
                    DbType = DbType.String,
                    Value = message.Header.Topic.Value
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}Timestamp",
                    DbType = DbType.DateTimeOffset,
                    Value = message.Header.TimeStamp.ToUniversalTime()
                }, //always store in UTC, as this is how we query messages
                new SqlParameter
                {
                    ParameterName = $"{prefix}CorrelationId",
                    DbType = DbType.String,
                    Value = message.Header.CorrelationId
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}ReplyTo",
                    DbType = DbType.String,
                    Value = message.Header.ReplyTo is not null ? message.Header.ReplyTo.Value : DBNull.Value
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}ContentType",
                    DbType = DbType.String,
                    Value = message.Header.ContentType is not null ? message.Header.ContentType.ToString() : DBNull.Value
                },
                new SqlParameter
                {
                    ParameterName = $"{prefix}PartitionKey",
                    DbType = DbType.String,
                    Value = message.Header.PartitionKey
                },
                new SqlParameter { ParameterName = $"{prefix}HeaderBag", Value = bagJson },
                _configuration.BinaryMessagePayload
                    ? new SqlParameter
                    {
                        ParameterName = $"{prefix}Body",
                        DbType = DbType.Binary,
                        Value = message.Body.Bytes
                    }
                    : new SqlParameter
                    {
                        ParameterName = $"{prefix}Body",
                        DbType = DbType.String,
                        Value = message.Body.Value
                    }
            ];
        }

        #endregion

        #region Property Extractors

        private static RoutingKey GetTopic(DbDataReader dr) => new RoutingKey(dr.GetString(dr.GetOrdinal("Topic")));

        private static MessageType GetMessageType(DbDataReader dr) =>
            (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));

        private static string GetMessageId(DbDataReader dr) => dr.GetString(dr.GetOrdinal("MessageId"));

        private static string? GetContentType(DbDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal)) return null;

            var contentType = dr.GetString(ordinal);
            return contentType;
        }

        private static string? GetReplyTo(DbDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ReplyTo");
            if (dr.IsDBNull(ordinal)) return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private static Dictionary<string, object>? GetContextBag(DbDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            var dictionaryBag =
                JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        private static string? GetCorrelationId(DbDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal)) return null;

            var correlationId = dr.GetString(ordinal);
            return correlationId;
        }

        private static DateTimeOffset GetTimeStamp(DbDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Timestamp");
            var timeStamp = dr.IsDBNull(ordinal)
                ? DateTimeOffset.MinValue
                : dr.GetDateTime(ordinal);
            return timeStamp;
        }

        private static string? GetPartitionKey(DbDataReader dr)
        {
            var ordinal = dr.GetOrdinal("PartitionKey");
            if (dr.IsDBNull(ordinal)) return null;

            var partitionKey = dr.GetString(ordinal);
            return partitionKey;
        }

        private static byte[]? GetBodyAsBytes(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Body");
            if (dr.IsDBNull(ordinal)) return null;

            var body = dr.GetStream(ordinal);
            if (body is MemoryStream memoryStream) // No need to dispose a MemoryStream, I do not think they dare to ever change that
                return memoryStream.ToArray(); // Then we can just return its value, instead of copying manually

            MemoryStream ms = new();
            body.CopyTo(ms);
            body.Dispose();
            return ms.ToArray();
        }

        private static string? GetBodyAsText(DbDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Body");
            return dr.IsDBNull(ordinal) ? null : dr.GetString(ordinal);
        }

        #endregion

        #region DataReader Operators

        protected override Message MapFunction(DbDataReader dr)
        {
            Message? message = null;
            if (dr.Read())
            {
                message = MapAMessage(dr);
            }

            dr.Close();
            return message ?? new Message();
        }

        protected override async Task<Message> MapFunctionAsync(DbDataReader dr, CancellationToken cancellationToken)
        {
            Message? message = null;
            if (await dr.ReadAsync(cancellationToken))
            {
                message = MapAMessage(dr);
            }

#if NET462
            dr.Close();
#else
            await dr.CloseAsync();
#endif
            return message ?? new Message();
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

#if NET462
            dr.Close();
#else
            await dr.CloseAsync();
#endif
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

#if NET462
            dr.Close();
#else
            await dr.CloseAsync();
#endif
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

        #endregion

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
                messageId: new Id(id),
                topic: topic,
                messageType: messageType,
                timeStamp: timeStamp,
                handledCount: 0,
                delayed: TimeSpan.Zero,
                correlationId: correlationId is not null ? new Id(correlationId) : Id.Empty,
                replyTo: replyTo is not null ? new RoutingKey(replyTo) : RoutingKey.Empty,
                contentType: contentType is not null ? new ContentType(contentType) : new ContentType(MediaTypeNames.Text.Plain),
                partitionKey: partitionKey is not null ? new PartitionKey(partitionKey) : PartitionKey.Empty);

            Dictionary<string, object>? dictionaryBag = GetContextBag(dr);
            if (dictionaryBag != null)
            {
                foreach (var keyValue in dictionaryBag)
                {
                    header.Bag.Add(keyValue.Key, keyValue.Value);
                }
            }

#if NET462 
            var body = _configuration.BinaryMessagePayload
                ? new MessageBody(GetBodyAsBytes((SqlDataReader)dr), new ContentType(MediaTypeNames.Application.Octet), CharacterEncoding.Raw)
                : new MessageBody(GetBodyAsText(dr), new ContentType("applicaton/json"), CharacterEncoding.UTF8);
#else
            var body = _configuration.BinaryMessagePayload
                ? new MessageBody(GetBodyAsBytes((SqlDataReader)dr), new ContentType(MediaTypeNames.Application.Octet), CharacterEncoding.Raw)
                : new MessageBody(GetBodyAsText(dr), new ContentType(MediaTypeNames.Application.Json), CharacterEncoding.UTF8);
#endif
            return new Message(header, body);
        }
    }
}
