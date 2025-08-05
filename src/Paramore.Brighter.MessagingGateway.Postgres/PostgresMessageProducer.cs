using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.MessagingGateway.Postgres;

/// <summary>
/// A message producer for writing messages to a PostgreSQL message queue.
/// Implements both synchronous and asynchronous interfaces for sending messages,
/// with optional delays.
/// </summary>
public partial class PostgresMessageProducer(
    RelationalDatabaseConfiguration configuration,
    PostgresPublication publication) : IAmAMessageProducerAsync, IAmAMessageProducerSync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgresMessageProducer>();
    private readonly PostgreSqlConnectionProvider _connectionProvider = new(configuration);
    private PostgresPublication _publication = publication;

    private string SchemaName => _publication.SchemaName ?? configuration.SchemaName ?? "public";
    private string TableName => _publication.QueueStoreTable ?? configuration.QueueStoreTable;
    private string QueueName => _publication.Topic!.Value;
    private bool BinaryMessagePayload => _publication.BinaryMessagePayload ?? configuration.BinaryMessagePayload;
    
    private NpgsqlDbType MessagePayloadDbType => BinaryMessagePayload ? NpgsqlDbType.Jsonb : NpgsqlDbType.Json;
    
    /// <inheritdoc />
    public Publication Publication
    {
        get => _publication;
        set => _publication = (PostgresPublication)value ?? throw new ArgumentException("Publication cannot be null");
    }

    /// <inheritdoc />
    public Activity? Span { get; set; }
    
    /// <inheritdoc />
    public IAmAMessageScheduler? Scheduler { get; set; }
    
    /// <inheritdoc />
    public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (_publication is null)
        {
            throw new ConfigurationException("No publication specified for producer");
        }
        
        Log.PublishingMessage(s_logger, message.Header.Topic, message.Id, message.Body);
        
        await using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $"INSERT INTO \"{SchemaName}\".\"{TableName}\"(\"visible_timeout\", \"queue\", \"content\") VALUES (CURRENT_TIMESTAMP, $1, $2) RETURNING \"id\"";
        command.Parameters.Add(new NpgsqlParameter { Value = QueueName });
        command.Parameters.Add(new NpgsqlParameter { Value = JsonSerializer.Serialize(message, JsonSerialisationOptions.Options), NpgsqlDbType = MessagePayloadDbType});
        var id = await command.ExecuteScalarAsync(cancellationToken);
        
        Log.PublishedMessage(s_logger, message.Header.Topic, message.Id, Convert.ToInt64(id));
    }

    /// <inheritdoc />
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
    {
        if (delay is null || delay == TimeSpan.Zero)
        {
            await SendAsync(message, cancellationToken);
            return;
        }
        
        if (_publication is null)
        {
            throw new ConfigurationException("No publication specified for producer");
        }
        
        Log.PublishingMessage(s_logger, message.Header.Topic, message.Id, message.Body);
        
        await using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $"INSERT INTO \"{SchemaName}\".\"{TableName}\"(\"visible_timeout\", \"queue\", \"content\") VALUES (CURRENT_TIMESTAMP + $1, $2, $3) RETURNING \"id\"";
        command.Parameters.Add(new NpgsqlParameter { Value = delay.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = QueueName });
        command.Parameters.Add(new NpgsqlParameter { Value = JsonSerializer.Serialize(message, JsonSerialisationOptions.Options), NpgsqlDbType = MessagePayloadDbType});
        var id = await command.ExecuteScalarAsync(cancellationToken);
        
        Log.PublishedMessage(s_logger, message.Header.Topic, message.Id, Convert.ToInt64(id));
    }
    
    /// <inheritdoc />
    public void Send(Message message)
    { 
        if (_publication is null)
        {
            throw new ConfigurationException("No publication specified for producer");
        }
        
        Log.PublishingMessage(s_logger, message.Header.Topic, message.Id, message.Body);
        
        using var connection = _connectionProvider.GetConnection();
        using var command = connection.CreateCommand();

        command.CommandText = $"INSERT INTO \"{SchemaName}\".\"{TableName}\"(\"visible_timeout\", \"queue\", \"content\") VALUES (CURRENT_TIMESTAMP, $1, $2) RETURNING \"id\"";
        command.Parameters.Add(new NpgsqlParameter { Value = QueueName });
        command.Parameters.Add(new NpgsqlParameter { Value = JsonSerializer.Serialize(message, JsonSerialisationOptions.Options), NpgsqlDbType = MessagePayloadDbType});
        var id = command.ExecuteScalar();
        
        Log.PublishedMessage(s_logger, message.Header.Topic, message.Id, Convert.ToInt64(id));
    }

    /// <inheritdoc />
    public void SendWithDelay(Message message, TimeSpan? delay)
    {
        if (delay is null || delay == TimeSpan.Zero)
        {
            Send(message);
            return;
        }
        
        if (_publication is null)
        {
            throw new ConfigurationException("No publication specified for producer");
        }
        
        Log.PublishingMessage(s_logger, message.Header.Topic, message.Id, message.Body);
        
        using var connection = _connectionProvider.GetConnection();
        using var command = connection.CreateCommand();

        command.CommandText = $"INSERT INTO \"{SchemaName}\".\"{TableName}\"(\"visible_timeout\", \"queue\", \"content\") VALUES (CURRENT_TIMESTAMP + $1, $2, $3) RETURNING \"id\"";
        command.Parameters.Add(new NpgsqlParameter { Value = delay.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = QueueName });
        command.Parameters.Add(new NpgsqlParameter { Value = JsonSerializer.Serialize(message, JsonSerialisationOptions.Options), NpgsqlDbType = MessagePayloadDbType});
        var id = command.ExecuteScalar();
        
        Log.PublishedMessage(s_logger, message.Header.Topic, message.Id, Convert.ToInt64(id));
    }


    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
    
    /// <inheritdoc />
    public void Dispose() => Span?.Dispose();

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "PostgresMessageProducer: Publishing message with topic {Topic} and id {Id} and message: {Request}")]
        public static partial void PublishingMessage(ILogger logger, string topic, string id, MessageBody request);

        [LoggerMessage(LogLevel.Debug, "PostgresMessageProducer: Published message with topic {Topic}, Brighter messageId {MessageId} and SNS messageId {PostgresMessageId}")]
        public static partial void PublishedMessage(ILogger logger, string topic, string messageId, long postgresMessageId);
    }
}
