using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Paramore.Brighter.Transformers.JustSaying.Extensions;

internal static class JsonNodeExtensions
{
    public static string? GetString(this JsonNode node, string key, string? defaultValue = null)
    {
        var element = node[key];
        if (element != null
            && element.GetValueKind() == JsonValueKind.String)
        {
            return element.GetValue<string>();
        }

        return defaultValue;
    }

    public static Guid GetGuid(this JsonNode node, string key)
    {
        var val = GetString(node, key);
        if (!string.IsNullOrEmpty(val) && Guid.TryParse(val, out var guid))
        {
            return guid;
        }
        
        return Guid.Empty;
    }

    public static Id? GetId(this JsonNode node, string key)
    {
        var element = GetString(node, key);
        if (string.IsNullOrEmpty(element))
        {
            return null;
        }

        return Id.Create(element);
    }

    public static DateTimeOffset GetDateTimeOffset(this JsonNode node, string key)
    {
        var element = GetString(node, key);
        if (DateTimeOffset.TryParse(element, out var datetime))
        {
            return datetime;
        }
        
        return DateTimeOffset.MinValue;
    }
}
