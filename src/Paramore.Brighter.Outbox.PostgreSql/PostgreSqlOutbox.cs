#region Licence

/* The MIT License (MIT)
Copyright © 2025 Jakub Syty <jakub.nekro@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
using Npgsql;
using NpgsqlTypes;
using Paramore.Brighter.Logging;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Outbox.PostgreSql
{
    /// <summary>
    /// Class PostgreSqlOutbox.
    /// </summary>
    public class PostgreSqlOutbox :
        RelationDatabaseOutboxSync<NpgsqlConnection, NpgsqlCommand, NpgsqlDataReader, NpgsqlParameter>
    {
        private readonly IPostgreSqlConnectionProvider _connectionProvider;
        
        /// <summary>
        ///     Initializes a new instance of the <see cref="PostgreSqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="connectionProvider">The connection factory.</param>
        public PostgreSqlOutbox(PostgreSqlOutboxConfiguration configuration, IPostgreSqlConnectionProvider connectionProvider) : base(
            configuration.OutboxTableName, new PostgreSqlQueries(), ApplicationLogging.CreateLogger<PostgreSqlOutbox>())
        {
            ContinueOnCapturedContext = false;
            _connectionProvider = connectionProvider;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="PostgreSqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public PostgreSqlOutbox(PostgreSqlOutboxConfiguration configuration) : this(configuration, new PostgreSqlNpgsqlConnectionProvider(configuration)) { }

        protected override void WriteToStore(IAmABoxTransactionConnectionProvider transactionConnectionProvider, Func<NpgsqlConnection, NpgsqlCommand> commandFunc, Action loggingAction)
        {
            var connectionProvider = GetConnectionProvider(transactionConnectionProvider);
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
                        loggingAction?.Invoke();
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

        protected override async Task WriteToStoreAsync(IAmABoxTransactionConnectionProvider transactionConnectionProvider, Func<NpgsqlConnection, NpgsqlCommand> commandFunc, Action loggingAction, CancellationToken cancellationToken)
        {
            var connectionProvider = GetConnectionProvider(transactionConnectionProvider);
            var connection = await connectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

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
                        loggingAction?.Invoke();
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

        protected override T ReadFromStore<T>(Func<NpgsqlConnection, NpgsqlCommand> commandFunc, Func<NpgsqlDataReader, T> resultFunc)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = connectionProvider.GetConnection();

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
                    if (!connectionProvider.IsSharedConnection)
                        connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction)
                        connection.Close();
                }
            }
        }

        protected override async Task<T> ReadFromStoreAsync<T>(Func<NpgsqlConnection, NpgsqlCommand> commandFunc, Func<NpgsqlDataReader, Task<T>> resultFunc, CancellationToken cancellationToken)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

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
                    if (!connectionProvider.IsSharedConnection)
                        connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction)
                        connection.Close();
                }
            }
        }

        protected override NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sqlText, int outBoxTimeout, params NpgsqlParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected override NpgsqlParameter[] CreatePagedOutstandingParameters(double milliSecondsSinceAdded, int pageSize, int pageNumber)
        {
            var parameters = new NpgsqlParameter[3];
            parameters[0] = CreateSqlParameter("PageNumber", pageNumber);
            parameters[1] = CreateSqlParameter("PageSize", pageSize);
            parameters[2] = CreateSqlParameter("OutstandingSince", milliSecondsSinceAdded);

            return parameters;
        }

        protected override NpgsqlParameter CreateSqlParameter(string parameterName, object value)
        {
            return new NpgsqlParameter(parameterName, value ?? DBNull.Value);
        }
        
        protected override NpgsqlParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new[]
            {
                CreateSqlParameter($"{prefix}MessageId", message.Id),
                CreateSqlParameter($"{prefix}MessageType", message.Header.MessageType.ToString()),
                CreateSqlParameter($"{prefix}Topic", message.Header.Topic),
                new NpgsqlParameter($"{prefix}Timestamp", NpgsqlDbType.TimestampTz) { Value = message.Header.TimeStamp.ToUniversalTime() }, //always store in UTC, as this is how we query messages
                CreateSqlParameter($"{prefix}CorrelationId", message.Header.CorrelationId),
                CreateSqlParameter($"{prefix}ReplyTo", message.Header.ReplyTo),
                CreateSqlParameter($"{prefix}ContentType", message.Header.ContentType),
                CreateSqlParameter($"{prefix}HeaderBag", bagJson),
                CreateSqlParameter($"{prefix}Body", message.Body?.Value)
            };
        }

        private static string GetTopic(NpgsqlDataReader dr) => dr.GetString(dr.GetOrdinal("Topic"));

        private static MessageType GetMessageType(NpgsqlDataReader dr) => (MessageType) Enum.Parse(typeof (MessageType), dr.GetString(dr.GetOrdinal("MessageType")));

        private static Guid GetMessageId(NpgsqlDataReader dr) => dr.GetGuid(dr.GetOrdinal("MessageId"));

        private string GetContentType(NpgsqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal)) return null; 
            
            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private string GetReplyTo(NpgsqlDataReader dr)
        {
             var ordinal = dr.GetOrdinal("ReplyTo");
             if (dr.IsDBNull(ordinal)) return null; 
             
             var replyTo = dr.GetString(ordinal);
             return replyTo;
        }

        private static Dictionary<string, object> GetContextBag(NpgsqlDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            var dictionaryBag = JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        private Guid? GetCorrelationId(NpgsqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal)) return null; 
            
            var correlationId = dr.GetGuid(ordinal);
            return correlationId;
        }

        private static DateTime GetTimeStamp(NpgsqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Timestamp");
            var timeStamp = dr.IsDBNull(ordinal)
                ? DateTime.MinValue
                : dr.GetDateTime(ordinal);
            return timeStamp;
        }

        protected override Message MapFunction(NpgsqlDataReader dr)
        {
            Message message = null;
            if (dr.Read())
            {
                message = MapAMessage(dr);
            }
            dr.Close();

            return message ?? new Message();
        }
        
        protected override async Task<Message> MapFunctionAsync(NpgsqlDataReader dr, CancellationToken cancellationToken)
        {
            Message message = null;
            if (await dr.ReadAsync(cancellationToken))
            {
                message = MapAMessage(dr);
            }
            dr.Close();

            return message ?? new Message();
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

        protected override async Task<IEnumerable<Message>> MapListFunctionAsync(NpgsqlDataReader dr, CancellationToken cancellationToken)
        {
            var messages = new List<Message>();
            while (await dr.ReadAsync(cancellationToken))
            {
                messages.Add(MapAMessage(dr));
            }
            dr.Close();

            return messages;
        }

        protected override async Task<int> MapOutstandingCountAsync(NpgsqlDataReader dr, CancellationToken cancellationToken)
        {
            int outstandingMessages = -1;
            if(await dr.ReadAsync(cancellationToken))
            {
                outstandingMessages = dr.GetInt32(0);
            }
            dr.Close();
           
            return outstandingMessages;
        }

        private IPostgreSqlConnectionProvider GetConnectionProvider(IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var connectionProvider = _connectionProvider;

            if (transactionConnectionProvider != null)
            {
                if (transactionConnectionProvider is IPostgreSqlTransactionConnectionProvider provider)
                    connectionProvider = provider;
                else
                    throw new Exception($"{nameof(transactionConnectionProvider)} does not implement interface {nameof(IPostgreSqlTransactionConnectionProvider)}.");
            }

            return connectionProvider;
        }

        private Message MapAMessage(NpgsqlDataReader dr)
        {
            var id = GetMessageId(dr);
            var messageType = GetMessageType(dr);
            var topic = GetTopic(dr);

            var header = new MessageHeader(id, topic, messageType);

            //new schema....we've got the extra header information
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

            var bodyOrdinal = dr.GetOrdinal("Body");
            string messageBody = string.Empty;
            if (!dr.IsDBNull(bodyOrdinal))
                messageBody = dr.GetString(bodyOrdinal);
            var body = new MessageBody(messageBody);

            return new Message(header, body);
        }
    }
}
