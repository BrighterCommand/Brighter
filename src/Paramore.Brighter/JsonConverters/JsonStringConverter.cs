using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paramore.Brighter.JsonConverters
{
    public class JsonStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString()!;
                case JsonTokenType.Number:
                    return reader.GetInt32().ToString();
                default:
                    throw new JsonException($"Unable to convert Json Type {reader.TokenType} to String, Supported Types are String, Number.");
            }           
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            // For performance, lift up the writer implementation.
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value.AsSpan());
            }
        }
    }
}
