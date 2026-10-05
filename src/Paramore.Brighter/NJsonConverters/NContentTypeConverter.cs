#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia <irakli.gabisonia94@gmail.com>

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
using System.Net.Mime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Paramore.Brighter.NJsonConverters;

/// <summary>
/// Reads a <see cref="ContentType"/> and its MIME parameters from Newtonsoft.Json's object representation.
/// </summary>
/// <remarks>
/// Serialization uses Newtonsoft.Json's existing contracts and configured converters to preserve the persisted JSON format.
/// </remarks>
public class NContentTypeConverter : JsonConverter<ContentType>
{
    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    /// <exception cref="JsonSerializationException">A parameter entry has a null key or value.</exception>
    public override ContentType? ReadJson(
        JsonReader reader,
        Type objectType,
        ContentType? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        JsonConverter? configuredConverter = GetConfiguredConverter(objectType, serializer);
        if (configuredConverter != null && configuredConverter.CanRead && !ReferenceEquals(configuredConverter, this))
            return (ContentType?)configuredConverter.ReadJson(reader, objectType, existingValue, serializer);

        if (reader.TokenType == JsonToken.Null)
            return null;

        var json = JObject.Load(reader);
        JProperty? parameters = json.Property(nameof(ContentType.Parameters), StringComparison.OrdinalIgnoreCase);
        parameters?.Remove();

        var contentType = new ContentType();
        using (JsonReader contentTypeReader = json.CreateReader())
        {
            serializer.Populate(contentTypeReader, contentType);
        }

        if (parameters == null || parameters.Value.Type == JTokenType.Null)
            return contentType;

        // TrackingStringDictionary has no Add(DictionaryEntry) method for Newtonsoft.Json to populate.
        foreach (KeyValuePair<string, string?> parameter in parameters.Value.ToObject<KeyValuePair<string, string?>[]>(serializer)!)
        {
            if (parameter.Key == null || parameter.Value == null)
                throw new JsonSerializationException("ContentType parameters require non-null Key and Value strings.");

            contentType.Parameters[parameter.Key] = parameter.Value;
        }

        return contentType;
    }

    internal static JsonConverter? GetConfiguredConverter(Type objectType, JsonSerializer serializer)
        => serializer.ContractResolver.ResolveContract(objectType).Converter
           ?? serializer.Converters.FirstOrDefault(converter => converter.CanConvert(objectType));

    /// <summary>
    /// Serialization is handled by the default Newtonsoft.Json contract because <see cref="CanWrite"/> is false.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when called directly.</exception>
    public override void WriteJson(JsonWriter writer, ContentType? value, JsonSerializer serializer)
        => throw new NotSupportedException("ContentType serialization uses the default Newtonsoft.Json contract.");
}
