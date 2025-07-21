using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paramore.Brighter.Transformers.JustSaying.JsonConverters;

/// <summary>
/// The <see cref="IPAddress"/> <see cref="JsonConverter{T}"/>
/// </summary>
public class IpAddressConverter : JsonConverter<IPAddress>
{
    public override IPAddress? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var val = reader.GetString();
        if (string.IsNullOrEmpty(val))
        {
            return null;
        }
        
        return IPAddress.Parse(val);
    }

    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
