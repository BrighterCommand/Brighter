#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.AsyncAPI.Model;

namespace Paramore.Brighter.AsyncAPI
{
    public static class AsyncApiHostExtensions
    {
        private static readonly JsonSerializerOptions s_serializerOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static async Task<AsyncApiDocument> GenerateAsyncApiDocumentAsync(this IHost host, string outputPath, CancellationToken ct = default)
        {
            var generator = host.Services.GetService<IAmAnAsyncApiDocumentGenerator>();
            if (generator == null)
            {
                throw new InvalidOperationException(
                    "IAmAnAsyncApiDocumentGenerator is not registered. Call UseAsyncApi() on IBrighterBuilder during service configuration.");
            }

            var document = await generator.GenerateAsync(ct).ConfigureAwait(false);
            var json = JsonSerializer.Serialize(document, s_serializerOptions);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath, json, ct).ConfigureAwait(false);

            return document;
        }

        public static AsyncApiDocument GenerateAsyncApiDocument(this IHost host, string outputPath)
        {
            return host.GenerateAsyncApiDocumentAsync(outputPath).GetAwaiter().GetResult();
        }
    }
}
