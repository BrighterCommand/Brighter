#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia <irakli.gabisonia94@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including but not limited to the rights
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

using System.Net.Mime;
using Newtonsoft.Json;

namespace Paramore.Brighter.NJsonConverters;

// A read-only property converter would suppress the caller's configured writers.
internal sealed class NContentTypePropertyConverter : NContentTypeConverter
{
    public override bool CanWrite => true;

    public override void WriteJson(JsonWriter writer, ContentType? value, JsonSerializer serializer)
    {
        JsonConverter? configuredConverter = GetConfiguredConverter(value?.GetType() ?? typeof(ContentType), serializer);
        if (configuredConverter?.CanWrite == true)
        {
            // Re-entering the serializer for the same value would trigger its circular-reference check.
            configuredConverter.WriteJson(writer, value, serializer);
            return;
        }

        serializer.Serialize(writer, value);
    }
}
