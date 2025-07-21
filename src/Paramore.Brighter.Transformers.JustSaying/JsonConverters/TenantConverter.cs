using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paramore.Brighter.Transformers.JustSaying.JsonConverters;

/// <summary>
/// The <see cref="Tenant"/> <see cref="JsonConverter{T}"/>
/// </summary>
public class TenantConverter : JsonConverter<Tenant?>
{
    public override Tenant? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var val = reader.GetString();
        if (string.IsNullOrEmpty(val))
        {
            return null;
        }

        return new Tenant(val!);
    }

    public override void Write(Utf8JsonWriter writer, Tenant? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value.Value);
        }
    }
}
