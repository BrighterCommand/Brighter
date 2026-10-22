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

using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// The <c>System.Text.Json</c> response writer the health-check documentation shows for the operator endpoint. It writes the
/// overall status and, per entry, its name, status, tags, description and <c>Data</c>.
/// </summary>
public static class BrighterHealthReportJsonWriter
{
    /// <summary>
    /// Writes the report as a JSON document.
    /// </summary>
    /// <param name="context">The HTTP context whose response receives the document.</param>
    /// <param name="report">The report to write.</param>
    /// <returns>A task that completes when the document is written.</returns>
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        await using var writer = new Utf8JsonWriter(context.Response.Body, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("status", report.Status.ToString());
        writer.WriteStartArray("entries");

        foreach (var (name, entry) in report.Entries)
        {
            writer.WriteStartObject();
            writer.WriteString("name", name);
            writer.WriteString("status", entry.Status.ToString());

            writer.WriteStartArray("tags");
            foreach (var tag in entry.Tags)
            {
                writer.WriteStringValue(tag);
            }
            writer.WriteEndArray();

            writer.WriteString("description", entry.Description);

            writer.WriteStartObject("data");
            foreach (var (key, value) in entry.Data)
            {
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object));
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync();
    }
}
