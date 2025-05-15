using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Paramore.Brighter.Logging;
using Paramore.Brighter.PostgreSql;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Paramore.Brighter.MessagingGateway.Postgres;

/// <summary>
/// A message consumer for reading messages from and managing a PostgreSQL message queue.
/// Implements both synchronous and asynchronous interfaces for consuming messages.
/// </summary>
public partial class PostgresMessageConsumer(
    RelationalDatabaseConfiguration configuration,
    PostgresSubscription subscription 
    ) : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgresMessageConsumer>();
    private readonly PostgreSqlConnectionProvider _connectionProvider = new(configuration);

    private string SchemaName => subscription.SchemaName ?? configuration.SchemaName ?? "public";
    private string TableName => subscription.QueueStoreTable ?? configuration.QueueStoreTable;
    private string QueueName => subscription.ChannelName.Value;
    private bool BinaryMessagePayload => subscription.BinaryMessagePayload ?? configuration.BinaryMessagePayload;
    private int BufferSize => subscription.BufferSize;
    private TimeSpan VisibleTimeout => subscription.VisibleTimeout;
    private bool HasLargeMessage => subscription.TableWithLargeMessage;
    
    private NpgsqlDbType DbType => BinaryMessagePayload ? NpgsqlDbType.Jsonb : NpgsqlDbType.Json;

    /// <inheritdoc />
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle))
        {
            return;
        }

        try
        {
            await using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM \"{SchemaName}\".\"{TableName}\" WHERE \"id\" = $1";
            command.Parameters.Add(new NpgsqlParameter { Value = receiptHandle });
            await command.ExecuteNonQueryAsync(cancellationToken);
            Log.DeletedMessage(s_logger, message.Id, receiptHandle, QueueName);
        }
        catch (Exception exception)
        {
            Log.ErrorDeletingMessage(s_logger, exception, message.Id, receiptHandle, QueueName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RejectAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle))
        {
            return;
        }

        try
        {
            Log.RejectingMessage(s_logger, message.Id, receiptHandle, QueueName);
            await using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM \"{SchemaName}\".\"{TableName}\" WHERE \"id\" = $1";
            command.Parameters.Add(new NpgsqlParameter { Value = receiptHandle });
        }
        catch (Exception exception)
        {
            Log.ErrorRejectingMessage(s_logger, exception, message.Id, receiptHandle, QueueName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Log.PurgingQueue(s_logger, TableName);
            await using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM \"{SchemaName}\".\"{TableName}\" WHERE \"queue\" = $1";
            command.Parameters.Add(new NpgsqlParameter { Value = QueueName });
            await command.ExecuteNonQueryAsync(cancellationToken);
            
            Log.PurgedQueue(s_logger, QueueName);
        }
        catch (Exception exception)
        {
            Log.ErrorPurgingQueue(s_logger, exception, QueueName);
            throw;
        }
        
    }

    /// <inheritdoc />
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        try
        {
            Log.RetrievingNextMessage(s_logger, QueueName);
            await using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            if (timeOut != null && timeOut != TimeSpan.Zero)
            {
                command.CommandTimeout = Convert.ToInt32(timeOut.Value.TotalSeconds);
            }
            
            command.CommandText = $"""
                                   UPDATE "{SchemaName}"."{TableName}" queue
                                   SET
                                       "visible_timeout" = CURRENT_TIMESTAMP + $1
                                   WHERE "id" IN ( 
                                       SELECT "id"
                                       FROM "{SchemaName}"."{TableName}" 
                                       WHERE "queue" = $2 AND "visible_timeout" <= CURRENT_TIMESTAMP
                                       ORDER BY "id"
                                       LIMIT {BufferSize}
                                       FOR UPDATE SKIP LOCKED 
                                   )
                                   RETURNING *
                                   """;

            command.Parameters.Add(new NpgsqlParameter { Value = VisibleTimeout });
            command.Parameters.Add(new NpgsqlParameter { Value = QueueName });
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            var messages = new List<Message>(BufferSize);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (HasLargeMessage)
                {
                    messages.Add(ToMessage(reader));
                }
                else
                {

                    messages.Add(await ToLargeMessageAsync(reader,cancellationToken));
                }
            }

            if (messages.Count == 0)
            {
                messages.Add(new Message());
            }

            return messages.ToArray();
        }
        catch (Exception exception)
        {
            Log.ErrorListeningToQueue(s_logger, exception, QueueName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle))
        {
            return false;
        }
        
        try
        {
            Log.RequeueingMessage(s_logger, message.Id);

            await using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            command.CommandText = $"UPDATE \"{SchemaName}\".\"{TableName}\" SET \"visible_timeout\" = CURRENT_TIMESTAMP + $1, \"content\" = $2 WHERE \"id\" = $3";
            command.Parameters.Add(new NpgsqlParameter { Value = delay ?? TimeSpan.Zero });
            command.Parameters.Add(new NpgsqlParameter { Value = JsonSerializer.Serialize(message, JsonSerialisationOptions.Options), NpgsqlDbType = DbType});
            command.Parameters.Add(new NpgsqlParameter { Value = receiptHandle });
            await command.ExecuteNonQueryAsync(cancellationToken);
            
            Log.RequeuedMessage(s_logger, message.Id);

            return true;
        }
        catch (Exception exception)
        {
            Log.ErrorRequeueingMessage(s_logger, exception, message.Id, receiptHandle, QueueName);
            return false;
        }
    }


    /// <inheritdoc />
    public void Acknowledge(Message message)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle))
        {
            return;
        }

        try
        {
            using var connection = _connectionProvider.GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM \"{SchemaName}\".\"{TableName}\" WHERE \"id\" = $1";
            command.Parameters.Add(new NpgsqlParameter { Value = receiptHandle });
            command.ExecuteNonQuery();
            Log.DeletedMessage(s_logger, message.Id, receiptHandle, QueueName);
        }
        catch (Exception exception)
        {
            Log.ErrorDeletingMessage(s_logger, exception, message.Id, receiptHandle, QueueName);
            throw;
        }
    }

    /// <inheritdoc />
    public void Reject(Message message)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle))
        {
            return;
        }

        try
        {
            Log.RejectingMessage(s_logger, message.Id, receiptHandle, QueueName);

            using var connection = _connectionProvider.GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM \"{SchemaName}\".\"{TableName}\" WHERE \"id\" = $1";
            command.Parameters.Add(new NpgsqlParameter { Value = receiptHandle });
            command.ExecuteNonQuery();       
        }
        catch (Exception exception)
        {
            Log.ErrorRejectingMessage(s_logger, exception, message.Id, receiptHandle, QueueName);
            throw;
        }       
    }

    /// <inheritdoc />
    public void Purge()
    {
        try
        {
            Log.PurgingQueue(s_logger, QueueName);
            using var connection = _connectionProvider.GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM \"{SchemaName}\".\"{TableName}\" WHERE \"queue\" = $1";
            command.Parameters.Add(new NpgsqlParameter { Value = QueueName });
            command.ExecuteNonQuery();
            Log.PurgedQueue(s_logger, QueueName);
        }
        catch (Exception exception)
        {
            Log.ErrorPurgingQueue(s_logger, exception, QueueName);
            throw;
        }
    }

    /// <inheritdoc />
    public Message[] Receive(TimeSpan? timeOut = null)
    {
        try
        {
            Log.RetrievingNextMessage(s_logger, QueueName);
            
            using var connection = _connectionProvider.GetConnection();
            using var command = connection.CreateCommand();
            if (timeOut != null && timeOut.Value != TimeSpan.Zero)
            {
                command.CommandTimeout = Convert.ToInt32(timeOut.Value.Seconds);
            }

            command.CommandText = $"""
                                   UPDATE "{SchemaName}"."{TableName}" queue
                                   SET
                                       "visible_timeout" = CURRENT_TIMESTAMP + $1
                                   WHERE "id" IN ( 
                                       SELECT "id"
                                       FROM "{SchemaName}"."{TableName}" 
                                       WHERE "queue" = $2 AND "visible_timeout" <= CURRENT_TIMESTAMP
                                       ORDER BY "id"
                                       LIMIT {BufferSize}
                                       FOR UPDATE SKIP LOCKED 
                                   )
                                   RETURNING *
                                   """;

            command.Parameters.Add(new NpgsqlParameter { Value = VisibleTimeout });
            command.Parameters.Add(new NpgsqlParameter { Value = QueueName });
            var reader = command.ExecuteReader();
            var messages = new List<Message>(BufferSize);
            while (reader.Read())
            {
                messages.Add(HasLargeMessage ? ToLargeMessage(reader) : ToMessage(reader));
            }
            
            if (messages.Count == 0)
            {
                messages.Add(new Message());
            }

            return messages.ToArray();
        }
        catch (Exception exception)
        {
            Log.ErrorListeningToQueue(s_logger, exception, QueueName);
            throw;
        }
    }

    /// <inheritdoc />
    public bool Requeue(Message message, TimeSpan? delay = null)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle))
        {
            return false;
        }
        
        try
        {
            Log.RequeueingMessage(s_logger, message.Id);

            using var connection = _connectionProvider.GetConnection();
            using var command = connection.CreateCommand();

            command.CommandText = $"UPDATE \"{SchemaName}\".\"{TableName}\" SET \"visible_timeout\" = CURRENT_TIMESTAMP + $1, \"content\" = $2 WHERE \"id\" = $3";

            command.Parameters.Add(new NpgsqlParameter { Value = delay ?? TimeSpan.Zero });
            command.Parameters.Add(new NpgsqlParameter { Value = JsonSerializer.Serialize(message, JsonSerialisationOptions.Options), NpgsqlDbType = DbType });
            command.Parameters.Add(new NpgsqlParameter { Value = receiptHandle });
            command.ExecuteNonQuery();
            
            Log.RequeuedMessage(s_logger, message.Id);

            return true;
        }
        catch (Exception exception)
        {
            Log.ErrorRequeueingMessage(s_logger, exception, message.Id, receiptHandle, QueueName);
            return false;
        }
    }
    
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _connectionProvider.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => _connectionProvider.Dispose();

    private static Message ToMessage(DbDataReader reader)
    {
        var id = reader.GetInt64(0);
        var content = reader.GetFieldValue<byte[]>(3);
        var message = JsonSerializer.Deserialize<Message>(content, JsonSerialisationOptions.Options)!;
        
        message.Header.Bag["ReceiptHandle"] = id;
        return message;
    }
    
    private static Message ToLargeMessage(DbDataReader reader)
    {
        var id = reader.GetInt64(0);

        var content = reader.GetStream(3);
        var message = JsonSerializer.Deserialize<Message>(content, JsonSerialisationOptions.Options)!;
        
        message.Header.Bag["ReceiptHandle"] = id;
        return message;
    }
    
    private static async Task<Message> ToLargeMessageAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var id = reader.GetInt64(0);

        var content = reader.GetStream(3);
        var message = await JsonSerializer.DeserializeAsync<Message>(content, JsonSerialisationOptions.Options, cancellationToken);
        
        message!.Header.Bag["ReceiptHandle"] = id;
        return message;
    }
    
    private static partial class Log 
    {
        [LoggerMessage(LogLevel.Information, "PostgresPullMessageConsumer: Deleted the message {Id} with receipt handle {ReceiptHandle} on the queue {QueueName}")]
        public static partial void DeletedMessage(ILogger logger, string id, object receiptHandle, string queueName);
        
        [LoggerMessage(LogLevel.Error, "PostgresPullMessageConsumer: Error during deleting the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}")]
        public static partial void ErrorDeletingMessage(ILogger logger, Exception exception, string id, object receiptHandle, string channelName);
        
        [LoggerMessage(LogLevel.Information, "PostgresPullMessageConsumer: Rejecting the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}")]
        public static partial void RejectingMessage(ILogger logger, string id, object? receiptHandle, string channelName);

        [LoggerMessage(LogLevel.Error, "PostgresPullMessageConsumer: Error during rejecting the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}")]
        public static partial void ErrorRejectingMessage(ILogger logger, Exception exception, string id, object? receiptHandle, string channelName);
        [LoggerMessage(LogLevel.Information, "PostgresPullMessageConsumer: Purging the queue {ChannelName}")]
        public static partial void PurgingQueue(ILogger logger, string channelName);

        [LoggerMessage(LogLevel.Information, "PostgresPullMessageConsumer: Purged the queue {ChannelName}")]
        public static partial void PurgedQueue(ILogger logger, string channelName);

        [LoggerMessage(LogLevel.Error, "PostgresPullMessageConsumer: Error purging queue {ChannelName}")]
        public static partial void ErrorPurgingQueue(ILogger logger, Exception exception, string channelName);

        [LoggerMessage(LogLevel.Debug, "PostgresPullMessageConsumer: Preparing to retrieve next message from queue {TableName}")]
        public static partial void RetrievingNextMessage(ILogger logger, string tableName);
        [LoggerMessage(LogLevel.Error, "PostgresPullMessageConsumer: There was an error listening to queue {ChannelName}")]
        public static partial void ErrorListeningToQueue(ILogger logger, Exception exception, string channelName);
        [LoggerMessage(LogLevel.Information, "PostgresPullMessageConsumer: re-queueing the message {Id}")]
        public static partial void RequeueingMessage(ILogger logger, string id);

        [LoggerMessage(LogLevel.Information, "PostgresPullMessageConsumer: re-queued the message {Id}")]
        public static partial void RequeuedMessage(ILogger logger, string id);

        [LoggerMessage(LogLevel.Error, "PostgresPullMessageConsumer: Error during re-queueing the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}")]
        public static partial void ErrorRequeueingMessage(ILogger logger, Exception exception, string id, object? receiptHandle, string channelName);

    }
}
