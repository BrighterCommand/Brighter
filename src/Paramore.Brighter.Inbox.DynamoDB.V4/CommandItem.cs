﻿using System;
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

    public CommandItem()
    {
        Time = $"{TimeStamp.Ticks}";
        CommandId = string.Empty;
        CommandBody = string.Empty;
    }

    public CommandItem(T command, string contextKey)
    {
        var type = typeof(T).Name;
            
        Time = $"{TimeStamp.Ticks}";
        CommandId = command.Id;
        CommandType = typeof(T).Name;
        CommandBody = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
        ContextKey = contextKey;
    }

    public T ConvertToCommand() => JsonSerializer.Deserialize<T>(CommandBody, JsonSerialisationOptions.Options)!;
}
