using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Paramore.Brighter.JsonConverters;
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
    PostgresSubscription subscription,
    RoutingKey? deadLetterRoutingKey = null,
    RoutingKey? invalidMessageRoutingKey = null
    ) : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgresMessageConsumer>();
    private readonly RelationalDatabaseConfiguration _configuration = configuration;
    private readonly PostgreSqlConnectionProvider _connectionProvider = new(configuration);
    private readonly RoutingKey? _deadLetterRoutingKey = deadLetterRoutingKey;
    private readonly RoutingKey? _invalidMessageRoutingKey = invalidMessageRoutingKey;
    // LazyThreadSafetyMode.None: message pumps are single-threaded per consumer, so no
    // thread-safety mode is needed. None does not cache exceptions, allowing the factory
    // to retry on the next .Value access after a transient failure.
    private readonly Lazy<PostgresMessageProducer?>? _deadLetterProducer =
        deadLetterRoutingKey != null ? new Lazy<PostgresMessageProducer?>(() => CreateProducer(configuration, deadLetterRoutingKey), LazyThreadSafetyMode.None) : null;
    private readonly Lazy<PostgresMessageProducer?>? _invalidMessageProducer =
        invalidMessageRoutingKey != null ? new Lazy<PostgresMessageProducer?>(() => CreateProducer(configuration, invalidMessageRoutingKey), LazyThreadSafetyMode.None) : null;

    private string SchemaName => subscription.SchemaName ?? _configuration.SchemaName ?? "public";
    private string TableName => subscription.QueueStoreTable ?? _configuration.QueueStoreTable;
    private string QueueName => subscription.ChannelName.Value;
    private bool BinaryMessagePayload => subscription.BinaryMessagePayload ?? _configuration.BinaryMessagePayload;
    private int BufferSize => subscription.BufferSize;
    private TimeSpan VisibleTimeout => subscription.VisibleTimeout;
    private bool HasLargeMessage => subscription.TableWithLargeMessage;
    
    private NpgsqlDbType DbType => BinaryMessagePayload ? NpgsqlDbType.Jsonb : NpgsqlDbType.Json;

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
                    messages.Add(await ToLargeMessageAsync(reader,cancellationToken));
                }
                else
                {
                    messages.Add(ToMessage(reader));
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
    public bool Reject(Message message, MessageRejectionReason? reason = null)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle))
        {
            return false;
        }

        Log.RejectingMessage(s_logger, message.Id, receiptHandle, QueueName);

        if (_deadLetterProducer == null && _invalidMessageProducer == null)
        {
            if (reason != null)
                Log.NoChannelsConfiguredForRejection(s_logger, message.Id, reason.RejectionReason.ToString());

            DeleteSourceMessage(receiptHandle);
            return true;
        }

        var rejectionReason = reason?.RejectionReason ?? RejectionReason.None;

        try
        {
            RefreshMetadata(message, reason);

            var (routingKey, shouldRoute, isFallingBackToDlq) = DetermineRejectionRoute(
                rejectionReason, _invalidMessageProducer != null, _deadLetterProducer != null);

            PostgresMessageProducer? producer = null;
            if (shouldRoute)
            {
                message.Header.Topic = routingKey!;
                if (isFallingBackToDlq)
                    Log.FallingBackToDlq(s_logger, message.Id);

                if (routingKey == _invalidMessageRoutingKey)
                    producer = _invalidMessageProducer?.Value;
                else if (routingKey == _deadLetterRoutingKey)
                    producer = _deadLetterProducer?.Value;
            }

            if (producer != null)
            {
                producer.Send(message);
                Log.MessageSentToRejectionChannel(s_logger, message.Id, rejectionReason.ToString());
            }
            else
            {
                Log.NoChannelsConfiguredForRejection(s_logger, message.Id, rejectionReason.ToString());
            }
        }
        catch (Exception ex)
        {
            // DLQ send failed — delete the source message (in finally) and return true
            // to prevent the message pump from retrying endlessly.
            Log.ErrorSendingToRejectionChannel(s_logger, ex, message.Id, rejectionReason.ToString());
            return true;
        }
        finally
        {
            DeleteSourceMessage(receiptHandle);
        }

        return true;
    }
    
    /// <inheritdoc />
    public async Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle))
        {
            return false;
        }

        Log.RejectingMessage(s_logger, message.Id, receiptHandle, QueueName);

        if (_deadLetterProducer == null && _invalidMessageProducer == null)
        {
            if (reason != null)
                Log.NoChannelsConfiguredForRejection(s_logger, message.Id, reason.RejectionReason.ToString());

            await DeleteSourceMessageAsync(receiptHandle, cancellationToken);
            return true;
        }

        var rejectionReason = reason?.RejectionReason ?? RejectionReason.None;

        try
        {
            RefreshMetadata(message, reason);

            var (routingKey, shouldRoute, isFallingBackToDlq) = DetermineRejectionRoute(
                rejectionReason, _invalidMessageProducer != null, _deadLetterProducer != null);

            PostgresMessageProducer? producer = null;
            if (shouldRoute)
            {
                message.Header.Topic = routingKey!;
                if (isFallingBackToDlq)
                    Log.FallingBackToDlq(s_logger, message.Id);

                if (routingKey == _invalidMessageRoutingKey)
                    producer = _invalidMessageProducer?.Value;
                else if (routingKey == _deadLetterRoutingKey)
                    producer = _deadLetterProducer?.Value;
            }

            if (producer != null)
            {
                await producer.SendAsync(message, cancellationToken);
                Log.MessageSentToRejectionChannel(s_logger, message.Id, rejectionReason.ToString());
            }
            else
            {
                Log.NoChannelsConfiguredForRejection(s_logger, message.Id, rejectionReason.ToString());
            }
        }
        catch (Exception ex)
        {
            // DLQ send failed — delete the source message and return true
            // to prevent the message pump from retrying endlessly.
            Log.ErrorSendingToRejectionChannel(s_logger, ex, message.Id, rejectionReason.ToString());
            return true;
        }
        finally
        {
            await DeleteSourceMessageAsync(receiptHandle, cancellationToken);
        }

        return true;
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
    
    private Message ToLargeMessage(DbDataReader reader)
    {
        var id = reader.GetInt64(0);

        using var content = reader.GetStream(3);
        if (DbType == NpgsqlDbType.Jsonb)
        {
            // Skipping the first by https://github.com/npgsql/npgsql/issues/6044
            content.Position = 1;
        }
        
        var message = JsonSerializer.Deserialize<Message>(content, JsonSerialisationOptions.Options)!;
        
        message.Header.Bag["ReceiptHandle"] = id;
        return message;
    }
    
    private async Task<Message> ToLargeMessageAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var id = reader.GetInt64(0);

        await using var content = reader.GetStream(3);
        if (DbType == NpgsqlDbType.Jsonb)
        {
            // Skipping the first by https://github.com/npgsql/npgsql/issues/6044
            content.Position = 1;
        }
        
        var message = await JsonSerializer.DeserializeAsync<Message>(content, JsonSerialisationOptions.Options, cancellationToken);
        
        message!.Header.Bag["ReceiptHandle"] = id;
        return message;
    }
    
    private static PostgresMessageProducer? CreateProducer(RelationalDatabaseConfiguration config, RoutingKey routingKey)
    {
        try
        {
            return new PostgresMessageProducer(config, new PostgresPublication { Topic = routingKey });
        }
        catch (Exception e)
        {
            Log.ErrorCreatingProducer(s_logger, e, routingKey.Value);
            return null;
        }
    }

    private static void RefreshMetadata(Message message, MessageRejectionReason? reason)
    {
        message.Header.Bag["originalTopic"] = message.Header.Topic.Value;
        message.Header.Bag["rejectionTimestamp"] = DateTimeOffset.UtcNow.ToString("o");
        message.Header.Bag["originalMessageType"] = message.Header.MessageType.ToString();

        if (reason == null) return;

        message.Header.Bag["rejectionReason"] = reason.RejectionReason.ToString();
        if (!string.IsNullOrEmpty(reason.Description))
            message.Header.Bag["rejectionMessage"] = reason.Description ?? string.Empty;
    }

    private (RoutingKey? routingKey, bool foundProducer, bool isFallingBackToDlq) DetermineRejectionRoute(
        RejectionReason rejectionReason,
        bool hasInvalidProducer,
        bool hasDeadLetterProducer)
    {
        switch (rejectionReason)
        {
            case RejectionReason.Unacceptable:
                if (hasInvalidProducer)
                    return (_invalidMessageRoutingKey, true, false);
                if (hasDeadLetterProducer)
                    return (_deadLetterRoutingKey, true, true);
                return (null, false, false);

            case RejectionReason.DeliveryError:
            case RejectionReason.None:
            default:
                if (hasDeadLetterProducer)
                    return (_deadLetterRoutingKey, true, false);
                return (null, false, false);
        }
    }

    private void DeleteSourceMessage(object receiptHandle)
    {
        try
        {
            using var connection = _connectionProvider.GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM \"{SchemaName}\".\"{TableName}\" WHERE \"id\" = $1";
            command.Parameters.Add(new NpgsqlParameter { Value = receiptHandle });
            command.ExecuteNonQuery();
            Log.DeletedMessage(s_logger, "source", receiptHandle, QueueName);
        }
        catch (Exception exception)
        {
            Log.ErrorDeletingMessage(s_logger, exception, "source", receiptHandle, QueueName);
            throw;
        }
    }

    private async Task DeleteSourceMessageAsync(object receiptHandle, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM \"{SchemaName}\".\"{TableName}\" WHERE \"id\" = $1";
            command.Parameters.Add(new NpgsqlParameter { Value = receiptHandle });
            await command.ExecuteNonQueryAsync(cancellationToken);
            Log.DeletedMessage(s_logger, "source", receiptHandle, QueueName);
        }
        catch (Exception exception)
        {
            Log.ErrorDeletingMessage(s_logger, exception, "source", receiptHandle, QueueName);
            throw;
        }
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

        [LoggerMessage(LogLevel.Warning, "PostgresPullMessageConsumer: No DLQ or invalid message channels configured for message {MessageId}, rejection reason: {RejectionReason}")]
        public static partial void NoChannelsConfiguredForRejection(ILogger logger, string messageId, string rejectionReason);

        [LoggerMessage(LogLevel.Information, "PostgresPullMessageConsumer: Message {MessageId} sent to rejection channel, reason: {RejectionReason}")]
        public static partial void MessageSentToRejectionChannel(ILogger logger, string messageId, string rejectionReason);

        [LoggerMessage(LogLevel.Warning, "PostgresPullMessageConsumer: Falling back to DLQ for message {MessageId}")]
        public static partial void FallingBackToDlq(ILogger logger, string messageId);

        [LoggerMessage(LogLevel.Error, "PostgresPullMessageConsumer: Error sending message {MessageId} to rejection channel, reason: {RejectionReason}")]
        public static partial void ErrorSendingToRejectionChannel(ILogger logger, Exception ex, string messageId, string rejectionReason);

        [LoggerMessage(LogLevel.Error, "PostgresPullMessageConsumer: Error creating producer for routing key {RoutingKey}")]
        public static partial void ErrorCreatingProducer(ILogger logger, Exception ex, string routingKey);
    }
}
