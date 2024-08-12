using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paramore.Brighter;

public class SubscriptionNameConverter : JsonConverter<SubscriptionName>
{
    public override SubscriptionName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return new SubscriptionName(reader.GetString());
            default:
                throw new JsonException($"Unable to convert Json Type {reader.TokenType} to String, Supported Types are String, Number.");
        }           
    }

    public override void Write(Utf8JsonWriter writer, SubscriptionName value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value.AsSpan());
        }
    }
}
