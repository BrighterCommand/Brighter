#region License
//Based on code byn Josef Ottoson. See: https://josef.codes/custom-dictionary-string-object-jsonconverter-for-system-text-json/
//itself derived from: https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to?WT.mc_id=DT-MVP-5004074&pivots=dotnet-5-0
#endregion

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paramore.Brighter.Serialization
{
    public class DictionaryStringObjectJsonConverter : JsonConverter<Dictionary<string, object?>>
    {
        public override Dictionary<string, object?> Read(ref Utf8JsonReader reader, Type? typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"JsonTokenType was of type {reader.TokenType}, only objects are supported");
            }

            var dictionary = new Dictionary<string, object?>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return dictionary;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("JsonTokenType was not PropertyName");
                }

                var propertyName = reader.GetString();

                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    throw new JsonException("Failed to get property name");
                }

                reader.Read();

                dictionary.Add(propertyName, ReadJsonElement(ref reader, options));
            }

            return dictionary;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, object?> dictionary, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var entry in dictionary)
            {
                var propertyName = entry.Key;
                var convertedPropName = options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;

                if (entry.Value != null)
                {
                    writer.WritePropertyName(convertedPropName);
                    var valueConverter = new ObjectToInferredTypesConverter();
                    valueConverter.Write(writer, entry.Value, options);
                }
                else
                {
                    writer.WriteNull(convertedPropName);
                }
            }

            writer.WriteEndObject();
        }

        private object? ReadJsonElement(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    if (reader.TryGetDateTime(out var date))
                    {
                        return date;
                    }

                    if (reader.TryGetGuid(out var guid))
                    {
                        return guid;
                    }
                    return reader.GetString();
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.Number:
                    if (reader.TryGetInt32(out var result))
                    {
                        return result;
                    }
                    return reader.GetDecimal();
                case JsonTokenType.StartObject:
                    return Read(ref reader, null, options);
                case JsonTokenType.StartArray:
                    var list = new List<object?>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        list.Add(ReadJsonElement(ref reader, options));
                    }
                    return list;
                default:
                    throw new JsonException($"'{reader.TokenType}' is not supported");
            }
        }
    }
}
