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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Neuroglia.AsyncApi;
using Neuroglia.AsyncApi.IO;
using Neuroglia.AsyncApi.v3;
using YamlDotNet.Serialization;

namespace Paramore.Brighter.AsyncAPI
{
    public static class AsyncApiHostExtensions
    {
        /// <summary>
        /// Generates the AsyncAPI document and writes it to both JSON and YAML files.
        /// The JSON file is written to <paramref name="outputPath"/>, and a corresponding
        /// YAML file is written alongside it (e.g. asyncapi.json → asyncapi.yaml).
        /// </summary>
        /// <remarks>
        /// JSON is treated as the canonical output. The YAML file is produced by re-parsing
        /// the JSON we just wrote and serialising the resulting tree as YAML, rather than
        /// asking Neuroglia's <see cref="IAsyncApiDocumentWriter"/> to serialise the CLR
        /// document directly to YAML.
        ///
        /// Why: Neuroglia's JSON and YAML serialisers (Neuroglia.Serialization 4.20.0,
        /// transitively pulled in by Neuroglia.AsyncApi.IO 3.0.6) apply different naming
        /// conventions to the same document model. The JSON path applies
        /// <c>JsonNamingPolicy.CamelCase</c>; the YAML path has its
        /// <c>WithNamingConvention(CamelCaseNamingConvention.Instance)</c> call commented
        /// out (see <c>YamlSerializer.DefaultSerializerConfiguration</c> in
        /// neuroglia-io/framework). The result is that property names without explicit
        /// <c>[YamlMember]</c> aliases (e.g. <c>IsReference</c> on
        /// <c>ReferenceableComponent&lt;T&gt;</c>, and embedded JSON Schema dictionary
        /// keys) leak through with PascalCase casing in YAML while JSON gets camelCase.
        /// Deriving YAML from the JSON output guarantees the two formats describe the
        /// same document.
        /// </remarks>
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

            var jsonPath = outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? outputPath
                : $"{outputPath}.json";
            var yamlPath = Path.ChangeExtension(jsonPath, ".yaml");

            // Write JSON via Neuroglia into a buffer so we can clean it up and derive YAML
            // from the same bytes. See the <remarks> on this method for rationale.
            byte[] rawJsonBytes;
            using (var jsonBuffer = new MemoryStream())
            {
                await writer.WriteAsync(document, jsonBuffer, AsyncApiDocumentFormat.Json, ct).ConfigureAwait(false);
                rawJsonBytes = jsonBuffer.ToArray();
            }

            // Build a single cleaned tree, then emit both formats from it. This strips
            // Neuroglia internals that leak into the wire (isReference) and removes
            // empty collections that the AsyncAPI spec treats as absent.
            using var rawDocument = JsonDocument.Parse(rawJsonBytes);
            var cleanedTree = CleanObjectTree(JsonElementToObject(rawDocument.RootElement));

            var cleanedJson = JsonSerializer.SerializeToUtf8Bytes(cleanedTree, s_jsonOutputOptions);
            await File.WriteAllBytesAsync(jsonPath, cleanedJson, ct).ConfigureAwait(false);

            var yamlText = SerializeAsYaml(cleanedTree);
            await File.WriteAllBytesAsync(yamlPath, Encoding.UTF8.GetBytes(yamlText), ct).ConfigureAwait(false);

            return document;
        }

        private static readonly JsonSerializerOptions s_jsonOutputOptions = new()
        {
            WriteIndented = true,
        };

        // Property keys that originate from Neuroglia's CLR model and have no place in an
        // AsyncAPI document on the wire. ReferenceableComponent&lt;T&gt; exposes IsReference
        // without [JsonIgnore], so System.Text.Json serialises it as "isReference" via the
        // camelCase policy.
        private static readonly HashSet<string> s_propertiesToStrip = new(StringComparer.Ordinal)
        {
            "isReference",
        };

        private static string SerializeAsYaml(object? tree)
        {
            var yamlSerializer = new SerializerBuilder()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .WithQuotingNecessaryStrings()
                .Build();

            return yamlSerializer.Serialize(tree!);
        }

        private static object? CleanObjectTree(object? node) => node switch
        {
            Dictionary<string, object?> map => CleanMap(map),
            List<object?> list => CleanList(list),
            _ => node,
        };

        private static Dictionary<string, object?> CleanMap(Dictionary<string, object?> map)
        {
            var cleaned = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var pair in map)
            {
                if (s_propertiesToStrip.Contains(pair.Key)) continue;

                var cleanedValue = CleanObjectTree(pair.Value);
                if (IsEmptyCollection(cleanedValue)) continue;

                cleaned[pair.Key] = cleanedValue;
            }

            return cleaned;
        }

        private static List<object?> CleanList(List<object?> list)
        {
            var cleaned = new List<object?>(list.Count);
            foreach (var item in list)
            {
                cleaned.Add(CleanObjectTree(item));
            }

            return cleaned;
        }

        private static bool IsEmptyCollection(object? value) => value switch
        {
            Dictionary<string, object?> map => map.Count == 0,
            List<object?> list => list.Count == 0,
            _ => false,
        };

        private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Object => ReadObject(element),
            JsonValueKind.Array => ReadArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ReadNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

        private static Dictionary<string, object?> ReadObject(JsonElement element)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                map[property.Name] = JsonElementToObject(property.Value);
            }

            return map;
        }

        private static List<object?> ReadArray(JsonElement element)
            => element.EnumerateArray().Select(JsonElementToObject).ToList();

        private static object ReadNumber(JsonElement element)
            => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble();
    }
}
