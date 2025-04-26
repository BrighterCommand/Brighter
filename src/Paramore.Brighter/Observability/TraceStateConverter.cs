using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paramore.Brighter.Observability;

public class TraceStateConverter : JsonConverter<TraceState>
{
    public override TraceState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected start of array");
        }

        var items = new List<KeyValuePair<string, string>>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected start of object");
            }

            string? key = null;
            string? value = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString()!;
                    reader.Read();

                    if (propertyName == "Key")
                    {
                        key = reader.GetString();
                    }
                    else if (propertyName == "Value")
                    {
                        value = reader.GetString();
                    }
                }
            }

            if (key != null && value != null)
            {
                items.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        var traceState = new TraceState();
        foreach (var item in items)
        {
            traceState.Add(item.Key, item.Value);
        }
        return traceState;
    }

    public override void Write(Utf8JsonWriter writer, TraceState value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var item in value)
        {
            writer.WriteStartObject();
            writer.WriteString("Key", item.Key);
            writer.WriteString("Value", item.Value!);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
