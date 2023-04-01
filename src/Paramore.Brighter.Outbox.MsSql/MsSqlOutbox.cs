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
        RelationDatabaseOutboxSync<SqlConnection, SqlCommand, SqlDataReader, SqlParameter>
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
        public MsSqlOutbox(MsSqlConfiguration configuration) : this(configuration, new MsSqlSqlAuthConnectionProvider(configuration)) { }

        protected override void WriteToStore(IAmABoxTransactionConnectionProvider transactionConnectionProvider, Func<SqlConnection, SqlCommand> commandFunc, Action loggingAction)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null && transactionConnectionProvider is IMsSqlTransactionConnectionProvider provider)
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

        protected override async Task WriteToStoreAsync(IAmABoxTransactionConnectionProvider transactionConnectionProvider, Func<SqlConnection, SqlCommand> commandFunc, Action loggingAction, CancellationToken cancellationToken)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null && transactionConnectionProvider is IMsSqlTransactionConnectionProvider provider)
                connectionProvider = provider;

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

        protected override T ReadFromStore<T>(Func<SqlConnection, SqlCommand> commandFunc, Func<SqlDataReader, T> resultFunc)
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

        protected override async Task<T> ReadFromStoreAsync<T>(Func<SqlConnection, SqlCommand> commandFunc, Func<SqlDataReader, Task<T>> resultFunc, CancellationToken cancellationToken)
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

        protected override SqlCommand CreateCommand(SqlConnection connection, string sqlText, int outBoxTimeout, params SqlParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected override SqlParameter[] CreatePagedOutstandingParameters(double milliSecondsSinceAdded, int pageSize, int pageNumber)
        {
            var parameters = new SqlParameter[3];
            parameters[0] = CreateSqlParameter("PageNumber", pageNumber);
            parameters[1] = CreateSqlParameter("PageSize", pageSize);
            parameters[2] = CreateSqlParameter("OutstandingSince", milliSecondsSinceAdded);

            return parameters;
        }

        #region Parameter Helpers

        protected override SqlParameter CreateSqlParameter(string parameterName, object value)
        {
            return new SqlParameter(parameterName, value ?? DBNull.Value);
        }

        protected override SqlParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new[]
            {
                CreateSqlParameter($"{prefix}MessageId", message.Id),
                CreateSqlParameter($"{prefix}MessageType", message.Header.MessageType.ToString()),
                CreateSqlParameter($"{prefix}Topic", message.Header.Topic),
                CreateSqlParameter($"{prefix}Timestamp", message.Header.TimeStamp.ToUniversalTime()), //always store in UTC, as this is how we query messages
                CreateSqlParameter($"{prefix}CorrelationId", message.Header.CorrelationId),
                CreateSqlParameter($"{prefix}ReplyTo", message.Header.ReplyTo),
                CreateSqlParameter($"{prefix}ContentType", message.Header.ContentType),
                CreateSqlParameter($"{prefix}HeaderBag", bagJson),
                CreateSqlParameter($"{prefix}Body", message.Body?.Value)
            };
        }

        #endregion

        #region Property Extractors

        private static string GetTopic(SqlDataReader dr) => dr.GetString(dr.GetOrdinal("Topic"));

        private static MessageType GetMessageType(SqlDataReader dr) => (MessageType) Enum.Parse(typeof (MessageType), dr.GetString(dr.GetOrdinal("MessageType")));

        private static Guid GetMessageId(SqlDataReader dr) => dr.GetGuid(dr.GetOrdinal("MessageId"));

        private string GetContentType(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal)) return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
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
            var dictionaryBag = JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
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

        protected override async Task<IEnumerable<Message>> MapListFunctionAsync(SqlDataReader dr, CancellationToken cancellationToken)
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
