using System;
using System.Text.Json;
using Amazon.DynamoDBv2.DataModel;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Inbox.DynamoDB.V4;

[DynamoDBTable("brighter_inbox")]
public class CommandItem<T> where T : class, IRequest
{
    public string Time { get; set; }

    [DynamoDBHashKey]
    [DynamoDBProperty]
    public string CommandId { get; set; }
        
    public string? CommandType { get; set; }
        
    public string CommandBody { get; set; }
        
    public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
        
    [DynamoDBRangeKey]
    [DynamoDBProperty]
    public string? ContextKey { get; set; } = null;

    /// <summary>
    /// The causation id that links this inbox entry to the outbox messages produced during the handler
    /// invocation that stored it. Null when the request was stored without a causation id.
    /// </summary>
    public string? CausationId { get; set; }

    public CommandItem()
    {
        Time = $"{TimeStamp.Ticks}";
        CommandId = string.Empty;
        CommandBody = string.Empty;
    }

    public CommandItem(T command, string contextKey, string? causationId = null)
    {
        var type = typeof(T).Name;

        Time = $"{TimeStamp.Ticks}";
        CommandId = command.Id.Value;
        CommandType = typeof(T).Name;
        CommandBody = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
        ContextKey = contextKey;
        CausationId = causationId;
    }

    public T ConvertToCommand() => JsonSerializer.Deserialize<T>(CommandBody, JsonSerialisationOptions.Options)!;
}

/// <summary>
/// A lightweight, non-generic projection of an inbox entry used to read its causation id without needing
/// the command type. Maps the same hash/range keys as <see cref="CommandItem{T}"/>.
/// </summary>
[DynamoDBTable("brighter_inbox")]
public class CausationItem
{
    [DynamoDBHashKey]
    [DynamoDBProperty]
    public string? CommandId { get; set; }

    [DynamoDBRangeKey]
    [DynamoDBProperty]
    public string? ContextKey { get; set; }

    public string? CausationId { get; set; }
}
