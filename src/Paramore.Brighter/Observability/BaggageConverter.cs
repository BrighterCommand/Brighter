using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Provides JSON serialization and deserialization support for W3C Baggage used in OpenTelemetry.
/// Baggage allows you to propagate user-defined key-value pairs through a distributed trace.
/// This converter handles the translation between Baggage objects and their JSON representation.
/// </summary>
/// <remarks>
/// The JSON format used is an array of objects, where each object has a Key and Value property.
/// For example: [{"Key":"user_id","Value":"123"},{"Key":"tenant","Value":"xyz" }]
/// </remarks>
public class BaggageConverter : JsonConverter<Baggage>
{
    /// <summary>
    /// Reads and converts a JSON array into a Baggage object.
    /// </summary>
    /// <param name="reader">The Utf8JsonReader to read from</param>
    /// <param name="typeToConvert">The type to convert to (Baggage)</param>
    /// <param name="options">The serializer options</param>
    /// <returns>A Baggage object containing the key-value pairs from the JSON</returns>
    /// <exception cref="JsonException">Thrown when the JSON structure is invalid</exception>
    public override Baggage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ValidateArrayStart(ref reader);
        var items = ReadKeyValuePairs(ref reader);
        return CreateBaggageFromItems(items);
    }

    private void ValidateArrayStart(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected start of array");
        }
    }

    private List<KeyValuePair<string, string>> ReadKeyValuePairs(ref Utf8JsonReader reader)
    {
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

            var kvp = ReadSingleKeyValuePair(ref reader);
            if (kvp.HasValue)
            {
                items.Add(kvp.Value);
            }
        }

        return items;
    }

    private KeyValuePair<string, string>? ReadSingleKeyValuePair(ref Utf8JsonReader reader)
    {
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
                var propertyName = reader.GetString()!;
                reader.Read();

                switch (propertyName)
                {
                    case "Key":
                        key = reader.GetString();
                        break;
                    case "Value":
                        value = reader.GetString();
                        break;
                }
            }
        }

        return (key != null && value != null) 
            ? new KeyValuePair<string, string>(key, value) 
            : null;
    }

    private Baggage CreateBaggageFromItems(List<KeyValuePair<string, string>> items)
    {
        var baggage = new Baggage();
        foreach (var item in items)
        {
            baggage.Add(item.Key, item.Value);
        }
        return baggage;
    }

    /// <summary>
    /// Writes a Baggage object as a JSON array.
    /// </summary>
    /// <param name="writer">The JSON writer to write to</param>
    /// <param name="value">The Baggage object to convert</param>
    /// <param name="options">The serializer options</param>
    public override void Write(Utf8JsonWriter writer, Baggage value, JsonSerializerOptions options)
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
