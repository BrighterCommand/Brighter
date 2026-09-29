#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paramore.Brighter.JsonConverters;

internal sealed class MonitorEventExceptionConverter : JsonConverter<Exception>
{
    public override Exception? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var details = JsonSerializer.Deserialize<ExceptionDetails>(ref reader, options);
        return details is null ? null : ToException(details);
    }

    public override void Write(Utf8JsonWriter writer, Exception value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, ToDetails(value), options);

    private static ExceptionDetails ToDetails(Exception exception)
        => new(exception.GetType().FullName, exception.Message, exception.StackTrace,
            exception.InnerException is null ? null : ToDetails(exception.InnerException));

    private static Exception ToException(ExceptionDetails details)
        => new(details.Message, details.InnerException is null ? null : ToException(details.InnerException));

    private sealed record ExceptionDetails(string? Type, string? Message, string? StackTrace,
        ExceptionDetails? InnerException);
}
