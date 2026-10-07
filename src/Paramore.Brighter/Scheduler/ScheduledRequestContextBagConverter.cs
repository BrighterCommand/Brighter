#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paramore.Brighter.Scheduler;

internal sealed class ScheduledRequestContextBagConverter : JsonConverter<Dictionary<string, object?>>
{
    // Fixed tags keep the wire format independent of CLR type names and application converters.
    private static readonly Dictionary<string, Type> s_types = new()
    {
        ["string"] = typeof(string),
        ["char"] = typeof(char),
        ["bool"] = typeof(bool),
        ["byte"] = typeof(byte),
        ["sbyte"] = typeof(sbyte),
        ["int16"] = typeof(short),
        ["uint16"] = typeof(ushort),
        ["int32"] = typeof(int),
        ["uint32"] = typeof(uint),
        ["int64"] = typeof(long),
        ["uint64"] = typeof(ulong),
        ["single"] = typeof(float),
        ["double"] = typeof(double),
        ["decimal"] = typeof(decimal),
        ["guid"] = typeof(Guid),
        ["datetime"] = typeof(DateTime),
        ["datetimeoffset"] = typeof(DateTimeOffset),
        ["bytes"] = typeof(byte[]),
        ["timespan"] = typeof(TimeSpan),
        ["uri"] = typeof(Uri)
    };
    private static readonly Dictionary<Type, string> s_tags = s_types.ToDictionary(entry => entry.Value, entry => entry.Key);
    private static readonly JsonSerializerOptions s_valueOptions = new();

    public override Dictionary<string, object?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("caseInsensitive", out var caseInsensitive)
            || caseInsensitive.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Object)
            throw new JsonException("Invalid scheduled request metadata.");

        var result = new Dictionary<string, object?>(caseInsensitive.GetBoolean()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var entry in values.EnumerateObject())
        {
            if (result.ContainsKey(entry.Name))
                throw new JsonException($"Duplicate scheduled request metadata key '{entry.Name}'.");

            if (entry.Value.ValueKind == JsonValueKind.Null)
            {
                result.Add(entry.Name, null);
                continue;
            }

            if (entry.Value.ValueKind != JsonValueKind.Object
                || !entry.Value.TryGetProperty("type", out var tag) || tag.ValueKind != JsonValueKind.String
                || !s_types.TryGetValue(tag.GetString()!, out var type)
                || !entry.Value.TryGetProperty("value", out var value) || value.ValueKind == JsonValueKind.Null)
                throw new JsonException($"Invalid scheduled request metadata for key '{entry.Name}'.");

            result.Add(entry.Name, JsonSerializer.Deserialize(value.GetRawText(), type, s_valueOptions));
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object?> value, JsonSerializerOptions options)
    {
        var caseInsensitive = value.Comparer.Equals(StringComparer.OrdinalIgnoreCase);
        if (!caseInsensitive && !value.Comparer.Equals(StringComparer.Ordinal)
            && !value.Comparer.Equals(EqualityComparer<string>.Default))
            throw new JsonException("Scheduled request metadata supports only ordinal key comparers.");

        writer.WriteStartObject();
        writer.WriteBoolean("caseInsensitive", caseInsensitive);
        writer.WriteStartObject("values");
        foreach (var entry in value)
        {
            writer.WritePropertyName(entry.Key);
            if (entry.Value == null)
            {
                writer.WriteNullValue();
                continue;
            }

            var type = entry.Value.GetType();
            if (!s_tags.TryGetValue(type, out var tag))
                throw new JsonException($"Unsupported scheduled request metadata type '{type}' for key '{entry.Key}'.");

            writer.WriteStartObject();
            writer.WriteString("type", tag);
            writer.WritePropertyName("value");
            try
            {
                JsonSerializer.Serialize(writer, entry.Value, type, s_valueOptions);
            }
            catch (ArgumentException exception)
            {
                throw new JsonException($"Invalid scheduled request metadata for key '{entry.Key}'.", exception);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
