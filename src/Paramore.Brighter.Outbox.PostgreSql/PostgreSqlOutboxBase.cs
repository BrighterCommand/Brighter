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

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Outbox.PostgreSql
{
    /// <summary>
    /// Base class containing common functionality for PostgreSQL outbox implementations
    /// </summary>
    public abstract class PostgreSqlOutboxBase
    {
        protected readonly PostgreSqlOutboxConfiguration _configuration;
        protected readonly IPostgreSqlConnectionProvider _connectionProvider;
        protected readonly string _outboxTableName;

        protected PostgreSqlOutboxBase(PostgreSqlOutboxConfiguration configuration, IPostgreSqlConnectionProvider connectionProvider = null)
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider ?? new PostgreSqlNpgsqlConnectionProvider(configuration);
            _outboxTableName = configuration.OutboxTableName;
        }

        protected IPostgreSqlConnectionProvider GetConnectionProvider(IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var connectionProvider = _connectionProvider ?? new PostgreSqlNpgsqlConnectionProvider(_configuration);

            if (transactionConnectionProvider != null)
            {
                if (transactionConnectionProvider is IPostgreSqlTransactionConnectionProvider provider)
                    connectionProvider = provider;
                else
                    throw new Exception($"{nameof(transactionConnectionProvider)} does not implement interface {nameof(IPostgreSqlTransactionConnectionProvider)}.");
            }

            return connectionProvider;
        }

        protected NpgsqlParameter InitNpgsqlParameter(string parametername, object value)
        {
            if (value != null)
                return new NpgsqlParameter(parametername, value);
            else
                return new NpgsqlParameter(parametername, DBNull.Value);
        }

        protected NpgsqlParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagjson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new NpgsqlParameter[]
            {
                InitNpgsqlParameter($"{prefix}MessageId", message.Id),
                InitNpgsqlParameter($"{prefix}MessageType", message.Header.MessageType.ToString()),
                InitNpgsqlParameter($"{prefix}Topic", message.Header.Topic),
                new NpgsqlParameter($"{prefix}Timestamp", NpgsqlDbType.TimestampTz) {Value = message.Header.TimeStamp},
                InitNpgsqlParameter($"{prefix}CorrelationId", message.Header.CorrelationId),
                InitNpgsqlParameter($"{prefix}ReplyTo", message.Header.ReplyTo),
                InitNpgsqlParameter($"{prefix}ContentType", message.Header.ContentType),
                InitNpgsqlParameter($"{prefix}HeaderBag", bagjson),
                InitNpgsqlParameter($"{prefix}Body", message.Body.Value)
            };
        }

        protected NpgsqlCommand InitPagedDispatchedCommand(NpgsqlConnection connection, double millisecondsDispatchedSince,
            int pageSize, int pageNumber)
        {
            var command = connection.CreateCommand();

            var parameters = new[]
            {
                InitNpgsqlParameter("PageNumber", pageNumber),
                InitNpgsqlParameter("PageSize", pageSize),
                InitNpgsqlParameter("OutstandingSince", -1 * millisecondsDispatchedSince)
            };

            command.CommandText = string.Format(PostgreSqlOutboxQueries.PagedDispatchedCommand, _configuration.OutboxTableName);
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected NpgsqlCommand InitPagedReadCommand(NpgsqlConnection connection, int pageSize, int pageNumber)
        {
            var command = connection.CreateCommand();

            var parameters = new[]
            {
                InitNpgsqlParameter("PageNumber", pageNumber),
                InitNpgsqlParameter("PageSize", pageSize)
            };

            command.CommandText = string.Format(PostgreSqlOutboxQueries.PagedReadCommand, _configuration.OutboxTableName);
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected NpgsqlCommand InitPagedOutstandingCommand(NpgsqlConnection connection, double milliSecondsSinceAdded, int pageSize,
            int pageNumber)
        {
            var command = connection.CreateCommand();

            var parameters = new[]
            {
                InitNpgsqlParameter("PageNumber", pageNumber),
                InitNpgsqlParameter("PageSize", pageSize),
                InitNpgsqlParameter("OutstandingSince", milliSecondsSinceAdded)
            };

            command.CommandText = string.Format(PostgreSqlOutboxQueries.PagedOutstandingCommand, _configuration.OutboxTableName);
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected NpgsqlCommand InitMarkDispatchedCommand(NpgsqlConnection connection, Guid messageId,
            DateTime? dispatchedAt)
        {
            var command = connection.CreateCommand();
            command.CommandText = string.Format(PostgreSqlOutboxQueries.MarkDispatchedCommand, _configuration.OutboxTableName);
            command.Parameters.Add(InitNpgsqlParameter("MessageId", messageId));
            command.Parameters.Add(InitNpgsqlParameter("DispatchedAt", dispatchedAt));
            return command;
        }

        protected NpgsqlCommand InitAddDbCommand(NpgsqlConnection connection, NpgsqlParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandText = string.Format(PostgreSqlOutboxQueries.AddMessageCommand, _configuration.OutboxTableName);
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected NpgsqlCommand InitBulkAddDbCommand(NpgsqlConnection connection, List<Message> messages)
        {
            var messageParams = new List<string>();
            var parameters = new List<NpgsqlParameter>();

            for (int i = 0; i < messages.Count; i++)
            {
                messageParams.Add($"(@p{i}_MessageId, @p{i}_MessageType, @p{i}_Topic, @p{i}_Timestamp, @p{i}_CorrelationId, @p{i}_ReplyTo, @p{i}_ContentType, @p{i}_HeaderBag, @p{i}_Body)");
                parameters.AddRange(InitAddDbParameters(messages[i], i));
            }
            var sql = string.Format(PostgreSqlOutboxQueries.BulkAddMessageCommand, _configuration.OutboxTableName, string.Join(",", messageParams));

            var command = connection.CreateCommand();

            command.CommandText = sql;
            command.Parameters.AddRange(parameters.ToArray());

            return command;
        }

        protected NpgsqlCommand InitDeleteDispatchedCommand(NpgsqlConnection connection, IEnumerable<Guid> messageIds)
        {
            var inClause = GenerateInClauseAndAddParameters(messageIds.ToList());
            foreach (var p in inClause.parameters)
            {
                p.DbType = DbType.Object;
            }
            return CreateCommand(connection, GenerateSqlText(PostgreSqlOutboxQueries.DeleteMessageCommand, inClause.inClause), 0,
                inClause.parameters);
        }

        protected (string inClause, NpgsqlParameter[] parameters) GenerateInClauseAndAddParameters(List<Guid> messageIds)
        {
            var paramNames = messageIds.Select((s, i) => "@p" + i).ToArray();

            var parameters = new NpgsqlParameter[messageIds.Count];
            for (int i = 0; i < paramNames.Count(); i++)
            {
                parameters[i] = CreateSqlParameter(paramNames[i], messageIds[i]);
            }

            return (string.Join(",", paramNames), parameters);
        }

        protected NpgsqlParameter CreateSqlParameter(string parameterName, object value)
        {
            return new NpgsqlParameter(parameterName, value ?? DBNull.Value);
        }

        protected string GenerateSqlText(string sqlFormat, params string[] orderedParams)
            => string.Format(sqlFormat, orderedParams.Prepend(_outboxTableName).ToArray());

        protected NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sqlText, int outBoxTimeout,
            params NpgsqlParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected Message MapAMessage(IDataReader dr)
        {
            var id = GetMessageId(dr);
            var messageType = GetMessageType(dr);
            var topic = GetTopic(dr);

            DateTime timeStamp = GetTimeStamp(dr);
            var correlationId = GetCorrelationId(dr);
            var replyTo = GetReplyTo(dr);
            var contentType = GetContentType(dr);

            var header = new MessageHeader(
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

            var body = new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

            return new Message(header, body);
        }

        protected static string GetTopic(IDataReader dr)
        {
            return dr.GetString(dr.GetOrdinal("Topic"));
        }

        protected static MessageType GetMessageType(IDataReader dr)
        {
            return (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
        }

        protected static Guid GetMessageId(IDataReader dr)
        {
            return dr.GetGuid(dr.GetOrdinal("MessageId"));
        }

        protected string GetContentType(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal))
                return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        protected string GetReplyTo(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ReplyTo");
            if (dr.IsDBNull(ordinal))
                return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        protected static Dictionary<string, object> GetContextBag(IDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            var dictionaryBag = JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        protected Guid? GetCorrelationId(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal))
                return null;

            var correlationId = dr.GetGuid(ordinal);
            return correlationId;
        }

        protected static DateTime GetTimeStamp(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Timestamp");
            var timeStamp = dr.IsDBNull(ordinal)
                ? DateTime.MinValue
                : dr.GetDateTime(ordinal);
            return timeStamp;
        }
    }
}
