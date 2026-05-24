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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Neuroglia.AsyncApi.v3;
using NJsonSchema;
using NJsonSchema.Generation;

namespace Paramore.Brighter.AsyncAPI.NJsonSchema
{
    /// <summary>
    /// Default <see cref="IAmASchemaGenerator"/> implementation that produces JSON Schema definitions
    /// using <c>NJsonSchema</c>. Property names are emitted in camelCase to match Brighter's default
    /// <c>JsonMessageMapper</c> wire format, and enums are described as strings.
    /// </summary>
    /// <remarks>
    /// NJsonSchema 11.x always serialises with a <c>$schema</c> dialect of
    /// <c>http://json-schema.org/draft-04/schema#</c> (see <c>JsonSchema.ToJson()</c> in the
    /// NJsonSchema source) — the dialect is hardcoded and not configurable. The
    /// <see cref="SchemaFormat"/> we advertise to AsyncAPI is set to match what we actually emit
    /// rather than declaring a draft the output does not conform to.
    /// </remarks>
    public sealed class NJsonSchemaGenerator : IAmASchemaGenerator
    {
        private const string SchemaFormat = "application/schema+json;version=draft-04";
        private static readonly JsonElement s_emptyObject;
        private static readonly SystemTextJsonSchemaGeneratorSettings s_settings = BuildSettings();
        private readonly ILogger<NJsonSchemaGenerator> _logger;

        static NJsonSchemaGenerator()
        {
            using var doc = JsonDocument.Parse("{}");
            s_emptyObject = doc.RootElement.Clone();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NJsonSchemaGenerator"/> class.
        /// </summary>
        /// <param name="logger">The logger used to record schema generation failures.</param>
        public NJsonSchemaGenerator(ILogger<NJsonSchemaGenerator> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<V3SchemaDefinition?> GenerateAsync(Type? requestType, CancellationToken ct = default)
        {
            if (requestType == null)
                return Task.FromResult<V3SchemaDefinition?>(new V3SchemaDefinition { SchemaFormat = SchemaFormat, Schema = s_emptyObject });

            try
            {
                var schema = JsonSchema.FromType(requestType, s_settings);
                var json = schema.ToJson();
                using var doc = JsonDocument.Parse(json);
                return Task.FromResult<V3SchemaDefinition?>(new V3SchemaDefinition { SchemaFormat = SchemaFormat, Schema = doc.RootElement.Clone() });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate JSON schema for type {TypeName}, returning empty object", requestType.FullName);
                return Task.FromResult<V3SchemaDefinition?>(new V3SchemaDefinition { SchemaFormat = SchemaFormat, Schema = s_emptyObject });
            }
        }

        /// <summary>
        /// Builds the NJsonSchema generator settings to mirror Brighter's default wire format:
        /// camelCase property names and enums serialised as strings.
        /// </summary>
        private static SystemTextJsonSchemaGeneratorSettings BuildSettings()
        {
            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            serializerOptions.Converters.Add(new JsonStringEnumConverter());

            return new SystemTextJsonSchemaGeneratorSettings
            {
                SerializerOptions = serializerOptions
            };
        }
    }
}
