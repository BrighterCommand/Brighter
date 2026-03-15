#region Licence
/* The MIT License (MIT)
Copyright © 2026 Jonny Olliff-Lee <jonny.ollifflee@gmail.com>

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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Neuroglia.AsyncApi;
using Neuroglia.AsyncApi.IO;
using Neuroglia.AsyncApi.v3;

namespace Paramore.Brighter.AsyncAPI
{
    public static class AsyncApiHostExtensions
    {
        /// <summary>
        /// Generates the AsyncAPI document and writes it to both JSON and YAML files.
        /// The JSON file is written to <paramref name="outputPath"/>, and a corresponding
        /// YAML file is written alongside it (e.g. asyncapi.json → asyncapi.yaml).
        /// </summary>
        public static async Task<V3AsyncApiDocument> GenerateAsyncApiDocumentAsync(this IHost host, string outputPath, CancellationToken ct = default)
        {
            var generator = host.Services.GetService<IAmAnAsyncApiDocumentGenerator>();
            if (generator == null)
            {
                throw new InvalidOperationException(
                    "IAmAnAsyncApiDocumentGenerator is not registered. Call UseAsyncApi() on IBrighterBuilder during service configuration.");
            }

            var writer = host.Services.GetService<IAsyncApiDocumentWriter>();
            if (writer == null)
            {
                throw new InvalidOperationException(
                    "IAsyncApiDocumentWriter is not registered. Ensure UseAsyncApi() calls AddAsyncApiIO() during service configuration.");
            }

            var document = await generator.GenerateAsync(ct).ConfigureAwait(false);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Determine JSON file path
            var jsonPath = outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? outputPath
                : $"{outputPath}.json";

            // Write JSON
            using (var jsonStream = new FileStream(jsonPath, FileMode.Create, FileAccess.Write))
            {
                await writer.WriteAsync(document, jsonStream, AsyncApiDocumentFormat.Json, ct).ConfigureAwait(false);
            }

            // Write YAML alongside the JSON file
            var yamlPath = Path.ChangeExtension(jsonPath, ".yaml");
            using (var yamlStream = new FileStream(yamlPath, FileMode.Create, FileAccess.Write))
            {
                await writer.WriteAsync(document, yamlStream, AsyncApiDocumentFormat.Yaml, ct).ConfigureAwait(false);
            }

            return document;
        }
    }
}
