#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Newtonsoft.Json;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.NJsonConverters;

/// <summary>
/// Newtonsoft.Json converter for <see cref="Baggage"/>. Mirrors the wire shape of the
/// System.Text.Json sibling <see cref="BaggageConverter"/>: a JSON array of objects with
/// <c>Key</c> and <c>Value</c> properties (e.g. <c>[{"Key":"user","Value":"alice"}]</c>),
/// so the two stacks can round-trip the same persisted document.
/// </summary>
public class NBaggageConverter : JsonConverter<Baggage>
{
    public override Baggage ReadJson(
        JsonReader reader,
        Type objectType,
        Baggage? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        var baggage = existingValue ?? new Baggage();

        if (reader.TokenType == JsonToken.Null)
        {
            return baggage;
        }

        if (reader.TokenType != JsonToken.StartArray)
        {
            throw new JsonSerializationException($"Expected StartArray for Baggage, got {reader.TokenType}");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.EndArray)
            {
                break;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonSerializationException($"Expected StartObject for Baggage entry, got {reader.TokenType}");
            }

            string? key = null;
            string? value = null;

            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName)
                {
                    continue;
                }

                var propertyName = (string)reader.Value!;
                reader.Read();

                switch (propertyName)
                {
                    case "Key":
                        key = reader.Value as string;
                        break;
                    case "Value":
                        value = reader.Value as string;
                        break;
                }
            }

            if (key != null && value != null)
            {
                baggage.Add(key, value);
            }
        }

        return baggage;
    }

    public override void WriteJson(JsonWriter writer, Baggage? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartArray();
        foreach (var entry in value)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Key");
            writer.WriteValue(entry.Key);
            writer.WritePropertyName("Value");
            writer.WriteValue(entry.Value);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }
}
