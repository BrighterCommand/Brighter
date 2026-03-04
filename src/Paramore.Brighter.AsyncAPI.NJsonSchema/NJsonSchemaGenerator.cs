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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NJsonSchema;

namespace Paramore.Brighter.AsyncAPI.NJsonSchema
{
    public sealed class NJsonSchemaGenerator : IAmASchemaGenerator
    {
        private static readonly JsonElement s_emptyObject;
        private readonly ILogger<NJsonSchemaGenerator> _logger;

        static NJsonSchemaGenerator()
        {
            using var doc = JsonDocument.Parse("{}");
            s_emptyObject = doc.RootElement.Clone();
        }

        public NJsonSchemaGenerator(ILogger<NJsonSchemaGenerator> logger)
        {
            _logger = logger;
        }

        public Task<JsonElement?> GenerateAsync(Type? requestType, CancellationToken ct = default)
        {
            if (requestType == null)
                return Task.FromResult<JsonElement?>(s_emptyObject);

            try
            {
                var schema = JsonSchema.FromType(requestType);
                var json = schema.ToJson();
                using var doc = JsonDocument.Parse(json);
                return Task.FromResult<JsonElement?>(doc.RootElement.Clone());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate JSON schema for type {TypeName}, returning empty object", requestType.FullName);
                return Task.FromResult<JsonElement?>(s_emptyObject);
            }
        }
    }
}
