using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Spanner;

namespace Paramore.Brighter.Outbox.Spanner;

/// <summary>
/// Implements the Brighter outbox pattern using Google Cloud Spanner.
/// This class extends <see cref="RelationDatabaseOutbox"/> to provide a reliable
/// mechanism for storing and tracking messages before they are dispatched to a message broker.
/// It ensures atomicity between the business transaction and message persistence.
/// </summary>
/// <remarks>
/// This outbox leverages Google Spanner's strong consistency and transactional capabilities
/// to guarantee that messages are durably saved to the outbox table. This prevents
/// message loss in scenarios where the application might fail after committing a
/// business transaction but before the message is successfully published.
/// <para>
/// It relies on a configured Spanner database and the provided connection provider
/// to interact with the outbox table. The <see cref="SpannerQueries"/> class
/// encapsulates the Spanner-specific SQL queries for outbox operations.
/// </para>
/// </remarks>
public class SpannerOutbox(IAmARelationalDatabaseConfiguration configuration, IAmARelationalDbConnectionProvider connectionProvider) 
    : RelationDatabaseOutbox(DbSystem.Spanner, configuration.DatabaseName, configuration.OutBoxTableName, new SpannerQueries(), s_logger)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpannerOutbox"/> class with only
    /// the database configuration. This constructor internally creates a default
    /// <see cref="SpannerConnectionProvider"/> based on the provided configuration.
    /// </summary>
    /// <param name="configuration">The configuration settings specific to the relational database,
    /// including connection string, database name, and outbox table name.</param>
    public SpannerOutbox(IAmARelationalDatabaseConfiguration configuration)
        : this(configuration, new SpannerConnectionProvider(configuration))
    {
        
    }
    
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SpannerOutbox>();

    /// <inheritdoc />
    protected override void WriteToStore(IAmABoxTransactionProvider<DbTransaction>? transactionProvider, Func<DbConnection, DbCommand> commandFunc, Action? loggingAction)
    {
        var connection = GetOpenConnection(connectionProvider, transactionProvider);
        using var command = commandFunc(connection);

        try
        {
            if (transactionProvider is { HasOpenTransaction: true })
            {
                command.Transaction = transactionProvider.GetTransaction();
            }

            command.ExecuteNonQuery();
        }
        catch (SpannerException ex) when (ex.ErrorCode == ErrorCode.AlreadyExists)
        {
            loggingAction?.Invoke();
        }
        finally
        {
            FinishWrite(connection, transactionProvider);
        }
    }

    /// <inheritdoc />
    protected override async Task WriteToStoreAsync(IAmABoxTransactionProvider<DbTransaction>? transactionProvider, Func<DbConnection, DbCommand> commandFunc, Action? loggingAction,
        CancellationToken cancellationToken)
    {
        var connection = await GetOpenConnectionAsync(connectionProvider, transactionProvider, cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
        
#if NETFRAMEWORK
        using var command = commandFunc(connection);
#else
        await using var command = commandFunc(connection);
#endif

        try
        {
            if (transactionProvider is { HasOpenTransaction: true })
            {
                command.Transaction = await transactionProvider
                    .GetTransactionAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
            }

            await command.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        catch (SpannerException ex) when (ex.ErrorCode == ErrorCode.AlreadyExists)
        {
            loggingAction?.Invoke();
        }
        finally
        {
            FinishWrite(connection, transactionProvider);
        }
    }

    /// <inheritdoc />
    protected override T ReadFromStore<T>(Func<DbConnection, DbCommand> commandFunc, Func<DbDataReader, T> resultFunc)
    {
        var connection = GetOpenConnection(connectionProvider, null);
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

    /// <inheritdoc />
    protected override async Task<T> ReadFromStoreAsync<T>(Func<DbConnection, DbCommand> commandFunc, Func<DbDataReader, Task<T>> resultFunc, CancellationToken cancellationToken)
    {
        var connection = await GetOpenConnectionAsync(connectionProvider, null, cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
        
#if NETFRAMEWORK 
        using var command = commandFunc.Invoke(connection);
#else
        await using var command = commandFunc.Invoke(connection);
#endif
        try
        {
            var read = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            
            return await resultFunc.Invoke(read)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
#if NETFRAMEWORK 
            connection.Close();
#else
            await connection.CloseAsync()
                .ConfigureAwait(ContinueOnCapturedContext);
#endif
        }
    }

    /// <inheritdoc />
    protected override DbCommand CreateCommand(DbConnection connection,
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

    /// <inheritdoc />
    protected override IDbDataParameter[] CreatePagedOutstandingParameters(TimeSpan since, int pageSize, int pageNumber)
    {
        return 
        [
            CreateSqlParameter("TimestampSince", DateTimeOffset.UtcNow.Subtract(since)),
            CreateSqlParameter("Take", pageSize),
            CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize)
        ];
    }

    /// <inheritdoc />
    protected override IDbDataParameter[] CreatePagedDispatchedParameters(TimeSpan dispatchedSince, int pageSize, int pageNumber)
    { 
        return 
        [
            CreateSqlParameter("DispatchedSince", DateTimeOffset.UtcNow.Subtract(dispatchedSince)),
            CreateSqlParameter("Take", pageSize),
            CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize)
        ];
    }

    /// <inheritdoc />
    protected override IDbDataParameter[] CreatePagedReadParameters(int pageSize, int pageNumber)
    {
        return 
        [
            CreateSqlParameter("Take", pageSize),
            CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize)
        ];
    }

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
    {
        return new SpannerParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };
    }

    /// <inheritdoc />
    protected override IDbDataParameter[] InitAddDbParameters(Message message, int? position = null)
    {
        var prefix = position.HasValue ? $"p{position}_" : "";
        var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
        
        object? replyTo = message.Header.ReplyTo?.Value;
        object? contentType = message.Header.ContentType?.ToString();
        object? dataSchema = message.Header.DataSchema?.ToString();
        object? subject = message.Header.Subject;
        object? traceParent = message.Header.TraceParent?.Value;
        object? traceState = message.Header.TraceState?.Value;

        return 
        [
            new SpannerParameter($"{prefix}MessageId", SpannerDbType.String, message.Id.Value),
            new SpannerParameter($"{prefix}MessageType", SpannerDbType.String, message.Header.MessageType.ToString()),
            new SpannerParameter($"{prefix}Topic", SpannerDbType.String, message.Header.Topic.Value),
            new SpannerParameter($"{prefix}Timestamp", SpannerDbType.Timestamp, message.Header.TimeStamp),
            new SpannerParameter($"{prefix}CorrelationId", SpannerDbType.String, message.Header.CorrelationId.Value),
            new SpannerParameter($"{prefix}ReplyTo", SpannerDbType.String, replyTo ?? DBNull.Value),
            new SpannerParameter($"{prefix}ContentType", SpannerDbType.String, contentType ?? DBNull.Value),
            new SpannerParameter($"{prefix}PartitionKey", SpannerDbType.String, message.Header.PartitionKey.Value),
            new SpannerParameter($"{prefix}HeaderBag", SpannerDbType.Json, bagJson),
            new SpannerParameter($"{prefix}Source", SpannerDbType.Json, message.Header.Source.ToString()),
            new SpannerParameter($"{prefix}Type", SpannerDbType.String, message.Header.Type),
            new SpannerParameter($"{prefix}DataSchema", SpannerDbType.String, dataSchema ?? DBNull.Value),
            new SpannerParameter($"{prefix}Subject", SpannerDbType.String, subject ?? DBNull.Value),
            new SpannerParameter($"{prefix}SpecVersion", SpannerDbType.String, message.Header.SpecVersion),
            new SpannerParameter($"{prefix}TraceParent", SpannerDbType.String, traceParent ?? DBNull.Value),
            new SpannerParameter($"{prefix}TraceState", SpannerDbType.String, traceState ?? DBNull.Value),
            new SpannerParameter($"{prefix}Baggage", SpannerDbType.String, message.Header.Baggage.ToString()),
            new SpannerParameter($"{prefix}Body", 
                configuration.BinaryMessagePayload ? SpannerDbType.Bytes : SpannerDbType.String,
                configuration.BinaryMessagePayload ? message.Body.Bytes : message.Body.Value),
        ];
    }

    /// <inheritdoc />
    protected override Message MapFunction(DbDataReader dr)
    {
         if (dr.Read())
         {
             return MapAMessage(dr);
         }

         return new Message();
    }

    /// <inheritdoc />
    protected override async Task<Message> MapFunctionAsync(DbDataReader dr, CancellationToken cancellationToken)
    {
        if (await dr.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
        {
            return MapAMessage(dr);
        }

        return new Message();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    protected override async Task<IEnumerable<Message>> MapListFunctionAsync(DbDataReader dr, CancellationToken cancellationToken)
    {
        var messages = new List<Message>();
        while (await dr.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
        {
            messages.Add(MapAMessage(dr));
        }

#if NETFRAMEWORK
        dr.Close();
#else
        await dr.CloseAsync().ConfigureAwait(ContinueOnCapturedContext);
#endif

        return messages;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    protected override async Task<int> MapOutstandingCountAsync(DbDataReader dr, CancellationToken cancellationToken)
    {
        int outstandingMessages = -1;
        if (await dr.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
        {
            outstandingMessages = dr.GetInt32(0);
        }

#if NETFRAMEWORK
        dr.Close();
#else
        await dr.CloseAsync().ConfigureAwait(ContinueOnCapturedContext);
#endif       

        return outstandingMessages;
    }

    private Message MapAMessage(DbDataReader dr)
    {
        var header = new MessageHeader(
            messageId: GetMessageId(dr),
            topic: GetTopic(dr),
            messageType: GetMessageType(dr),
            source: GetSource(dr),
            type: GetEventType(dr),
            timeStamp: GetTimeStamp(dr),
            correlationId: GetCorrelationId(dr),
            replyTo: GetReplyTo(dr),
            contentType: GetContentType(dr),
            partitionKey: GetPartitionKey(dr),
            dataSchema: GetDataSchema(dr),
            subject: GetSubject(dr),
            handledCount: 0, // HandledCount is zero when restored from the Outbox
            delayed: TimeSpan.Zero, // Delayed is zero when restored from the Outbox
            traceParent: GetTraceParent(dr),
            traceState: GetTraceState(dr),
            baggage: GetBaggage(dr)) { DataRef = GetDataRef(dr), SpecVersion = GetSpecVersion(dr) };

        var bag = GetContextBag(dr);
        if (bag != null)
        {
            foreach (var keyValue in bag)
            {
                header.Bag.Add(keyValue.Key, keyValue.Value);
            }
        }

        MessageBody body = configuration.BinaryMessagePayload ? 
            new MessageBody(dr.GetFieldValue<byte[]>(dr.GetOrdinal("Body"))) 
            : new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

        return new Message(header, body);
    }
    
    private static Id GetMessageId(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "MessageId", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return Id.Random;
        }
        
        var id = dr.GetString(ordinal);
        return new Id(id);
    }

    private static MessageType GetMessageType(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "MessageType", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return MessageType.MT_NONE;
        }

        var value = dr.GetString(ordinal);
        if (string.IsNullOrEmpty(value))
        {
            return MessageType.MT_NONE;
        }

#if NETFRAMEWORK
        return (MessageType)Enum.Parse(typeof(MessageType), value);
#else
        return Enum.Parse<MessageType>(value);
#endif
    }
    
    private static bool TryGetOrdinal(DbDataReader dr, string columnName, out int ordinal)
    {
        try
        {
            ordinal = dr.GetOrdinal(columnName);
            return true;
        }
        catch (IndexOutOfRangeException)
        {
            ordinal = -1;
            return false;
        }
    }
    
    private static RoutingKey GetTopic(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "Topic", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return RoutingKey.Empty;
        }
           
        var topic = dr.GetString(ordinal);
        if (string.IsNullOrEmpty(topic))
        {
            return RoutingKey.Empty;
        }
            
        return new RoutingKey(topic);
    }
    
    private static DateTimeOffset GetTimeStamp(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "Timestamp", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return DateTimeOffset.UtcNow;
        }
            
        var timeStamp = dr.IsDBNull(ordinal)
            ? DateTimeOffset.MinValue
            : dr.GetDateTime(ordinal);
        return timeStamp;
    }
    private static Id GetCorrelationId(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "CorrelationId", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return Id.Empty;
        }
            
        var correlationId = dr.GetString(ordinal);
        if (string.IsNullOrEmpty(correlationId))
        {
            return Id.Empty;
        }
            
        return new Id(correlationId);
    }
    
   private static Baggage GetBaggage(DbDataReader dr)
   {
       var baggage = new Baggage();
       if (!TryGetOrdinal(dr, "Baggage", out var ordinal) || dr.IsDBNull(ordinal))
       {
           return baggage;
       }

       var baggageString = dr.GetString(ordinal);
       if (string.IsNullOrEmpty(baggageString))
       {
           return baggage;
       }
           
       baggage.LoadBaggage(baggageString);
       return baggage;
    }

   private static ContentType GetContentType(DbDataReader dr)
   {
       if (!TryGetOrdinal(dr, "ContentType", out var ordinal) || dr.IsDBNull(ordinal))
       {
           return new ContentType(MediaTypeNames.Text.Plain);
       }
            
       var replyTo = dr.GetString(ordinal);
       if (string.IsNullOrEmpty(replyTo))
       {
           return new ContentType(MediaTypeNames.Text.Plain);
       }
            
       return new ContentType(replyTo);
    }

    private static Dictionary<string, object>? GetContextBag(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "HeaderBag", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return new Dictionary<string, object>();
        }

        var headerBag = dr.GetString(ordinal);
        var dictionaryBag = JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
        return dictionaryBag;
    }

        
    private static string GetDataRef(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "DataRef", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return string.Empty;
        }
            
        var dataRef = dr.GetString(ordinal);
        return string.IsNullOrEmpty(dataRef) ? string.Empty : dataRef;
    }
        
    private static Uri? GetDataSchema(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "DataRef", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return null;
        }
            
        var dataSchema = dr.GetString(ordinal);
        return string.IsNullOrEmpty(dataSchema) ? null : new Uri(dataSchema, UriKind.RelativeOrAbsolute);
    }
        
    private static string GetEventType(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "Type", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return MessageHeader.DefaultType;
        }
            
        var type = dr.GetString(ordinal);
        if (string.IsNullOrEmpty(type))
        {
            return MessageHeader.DefaultType;
        }
            
        return type;
    }
    
    private static PartitionKey GetPartitionKey(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "PartitionKey", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return PartitionKey.Empty;
        }

        var partitionKey = dr.GetString(ordinal);
        return new PartitionKey(partitionKey);
    }

    private static RoutingKey GetReplyTo(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "ReplyTo", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return RoutingKey.Empty;
        }

        var replyTo = dr.GetString(ordinal);
        if (string.IsNullOrEmpty(replyTo))
        {
            return RoutingKey.Empty;
        }
            
        return new RoutingKey(replyTo);
    }
        
    private static Uri GetSource(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "Source", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return new Uri(MessageHeader.DefaultSource);
        }
            
        var source = dr.GetString(ordinal);
        return string.IsNullOrEmpty(source) ? new Uri(MessageHeader.DefaultSource) : new Uri(source);
    }
        
    private static string GetSpecVersion(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "SpecVersion", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return MessageHeader.DefaultSpecVersion;
        }
            
        var specVersion = dr.GetString(ordinal);
        return string.IsNullOrEmpty(specVersion) ? MessageHeader.DefaultSpecVersion : specVersion;
    }
        
    private static string GetSubject(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "Subject", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return string.Empty;
        }
            
        var subject = dr.GetString(ordinal);
        return string.IsNullOrEmpty(subject) ? string.Empty : subject;
    }
    
    private static string GetTraceParent(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "TraceParent", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return string.Empty;
        }
            
        var traceParent = dr.GetString(ordinal);
        return string.IsNullOrEmpty(traceParent) ? string.Empty : traceParent;
    }
        
    private static string GetTraceState(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, "TraceState", out var ordinal) || dr.IsDBNull(ordinal))
        {
            return string.Empty;
        }
            
        var traceState = dr.GetString(ordinal);
        return string.IsNullOrEmpty(traceState) ? string.Empty : traceState;
    }
}
