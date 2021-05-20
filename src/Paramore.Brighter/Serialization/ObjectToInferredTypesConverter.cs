#region license

//From: https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to?WT.mc_id=DT-MVP-5004074&pivots=dotnet-5-0

#endregion

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paramore.Brighter.Serialization
{
    public class ObjectToInferredTypesConverter : JsonConverter<object>
    {
        public override object Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number when reader.TryGetInt64(out long l) => l,
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String when reader.TryGetDateTime(out DateTime datetime) => datetime,
            JsonTokenType.String => reader.GetString(),
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };

        public override void Write(
            Utf8JsonWriter writer,
            object objectToWrite,
            JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);
    }
}
