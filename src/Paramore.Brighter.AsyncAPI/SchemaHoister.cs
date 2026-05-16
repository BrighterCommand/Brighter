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

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Neuroglia.AsyncApi.v3;

namespace Paramore.Brighter.AsyncAPI
{
    // Lifts embedded { definitions: {...} } and { $defs: {...} } blocks from each message's
    // payload schema into a single shared pool placed at #/components/schemas. NJsonSchema
    // represents class inheritance via a per-message embedded definitions block, which
    // duplicates the base type once per message. Hoisting collapses that to one shared
    // entry and rewrites every $ref accordingly.
    //
    // Deduplication: by name, first-seen content wins. If two messages declare a
    // definitions entry under the same name with different content, the first content is
    // retained. This is acceptable for the common case of a class-inheritance base type
    // appearing under multiple derived messages, where the content is structurally
    // identical by construction.
    internal static class SchemaHoister
    {
        private const string HoistedRefPrefix = "#/components/schemas/";

        internal static Dictionary<string, V3SchemaDefinition> HoistEmbeddedDefinitions(
            Dictionary<string, V3MessageDefinition> messages)
        {
            var hoisted = new Dictionary<string, V3SchemaDefinition>(System.StringComparer.Ordinal);

            foreach (var key in messages.Keys.ToList())
            {
                var message = messages[key];
                var rewrittenPayload = LiftMessageDefinitions(message.Payload, hoisted);
                if (!ReferenceEquals(rewrittenPayload, message.Payload))
                {
                    messages[key] = new V3MessageDefinition
                    {
                        Name = message.Name,
                        ContentType = message.ContentType,
                        Reference = message.Reference,
                        Payload = rewrittenPayload,
                    };
                }
            }

            return hoisted;
        }

        private static V3SchemaDefinition? LiftMessageDefinitions(
            V3SchemaDefinition? schema,
            Dictionary<string, V3SchemaDefinition> hoisted)
        {
            if (schema?.Schema is not JsonElement payload)
            {
                return schema;
            }

            var root = JsonNode.Parse(payload.GetRawText());
            if (root is not JsonObject rootObject)
            {
                return schema;
            }

            ExtractDefinitionsInto(rootObject, "definitions", schema.SchemaFormat, hoisted);
            ExtractDefinitionsInto(rootObject, "$defs", schema.SchemaFormat, hoisted);

            RewriteRefs(rootObject);

            using var rewritten = JsonDocument.Parse(rootObject.ToJsonString());
            return new V3SchemaDefinition
            {
                SchemaFormat = schema.SchemaFormat,
                Schema = rewritten.RootElement.Clone(),
            };
        }

        private static void ExtractDefinitionsInto(
            JsonObject rootObject,
            string propertyName,
            string? schemaFormat,
            Dictionary<string, V3SchemaDefinition> hoisted)
        {
            if (!rootObject.TryGetPropertyValue(propertyName, out var defsNode) || defsNode is not JsonObject defs)
            {
                return;
            }

            foreach (var entry in defs.ToList())
            {
                if (entry.Value is null) continue;
                if (hoisted.ContainsKey(entry.Key)) continue;

                // Rewrite refs inside the hoisted definition so cross-definition $refs
                // (e.g. a BillingInfo schema referencing AddressInfo) resolve at the new
                // location under #/components/schemas/.
                var entryNode = JsonNode.Parse(entry.Value.ToJsonString())!;
                RewriteRefs(entryNode);

                using var entryDoc = JsonDocument.Parse(entryNode.ToJsonString());
                hoisted[entry.Key] = new V3SchemaDefinition
                {
                    SchemaFormat = schemaFormat,
                    Schema = entryDoc.RootElement.Clone(),
                };
            }

            rootObject.Remove(propertyName);
        }

        private static void RewriteRefs(JsonNode node)
        {
            switch (node)
            {
                case JsonObject obj:
                    RewriteRefsInObject(obj);
                    break;
                case JsonArray array:
                    RewriteRefsInArray(array);
                    break;
            }
        }

        private static void RewriteRefsInObject(JsonObject obj)
        {
            RewriteRefProperty(obj);

            foreach (var property in obj)
            {
                if (property.Value != null)
                {
                    RewriteRefs(property.Value);
                }
            }
        }

        private static void RewriteRefsInArray(JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                {
                    RewriteRefs(item);
                }
            }
        }

        private static void RewriteRefProperty(JsonObject obj)
        {
            if (!obj.TryGetPropertyValue("$ref", out var refNode) ||
                refNode is not JsonValue refValue ||
                !refValue.TryGetValue<string>(out var refString))
            {
                return;
            }

            if (refString.StartsWith("#/definitions/"))
            {
                obj["$ref"] = HoistedRefPrefix + refString.Substring("#/definitions/".Length);
            }
            else if (refString.StartsWith("#/$defs/"))
            {
                obj["$ref"] = HoistedRefPrefix + refString.Substring("#/$defs/".Length);
            }
        }
    }
}
