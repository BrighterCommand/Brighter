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
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Neuroglia;
using Neuroglia.AsyncApi.v3;

namespace Paramore.Brighter.AsyncAPI
{
    // SDK fluent builders (V3AsyncApiDocumentBuilder etc.) were evaluated but are not used here.
    // This generator builds documents dynamically: channels, operations, and messages are added
    // incrementally across multiple loops (subscriptions, publications, assembly scanning) with
    // deduplication via dictionary TryAdd. The fluent builder's nested Action<> delegates don't
    // simplify this pattern and would obscure the deduplication logic.
    public sealed class AsyncApiDocumentGenerator : IAmAnAsyncApiDocumentGenerator
    {
        private sealed record GenerationContext(
            Dictionary<string, V3ChannelDefinition> Channels,
            Dictionary<string, V3OperationDefinition> Operations,
            Dictionary<string, V3MessageDefinition> Messages,
            HashSet<(string ChannelId, string Action)> CoveredChannelActions);

        private readonly AsyncApiOptions _options;
        private readonly IAmASchemaGenerator _schemaGenerator;
        private readonly IEnumerable<Subscription>? _subscriptions;
        private readonly IEnumerable<Publication>? _publications;

        public AsyncApiDocumentGenerator(
            AsyncApiOptions options,
            IAmASchemaGenerator schemaGenerator,
            IEnumerable<Subscription>? subscriptions,
            IEnumerable<Publication>? publications)
        {
            _options = options;
            _schemaGenerator = schemaGenerator;
            _subscriptions = subscriptions;
            _publications = publications;
        }

        public async Task<V3AsyncApiDocument> GenerateAsync(CancellationToken ct = default)
        {
            var context = new GenerationContext(
                new Dictionary<string, V3ChannelDefinition>(),
                new Dictionary<string, V3OperationDefinition>(),
                new Dictionary<string, V3MessageDefinition>(),
                new HashSet<(string ChannelId, string Action)>());

            await AddSubscriptionsAsync(context, ct).ConfigureAwait(false);
            await AddPublicationsAsync(context, ct).ConfigureAwait(false);
            await AddFromAssemblyScanningAsync(context, ct).ConfigureAwait(false);

            var doc = new V3AsyncApiDocument
            {
                Info = new V3ApiInfo
                {
                    Title = _options.Title,
                    Version = _options.Version,
                    Description = _options.Description
                },
                Servers = _options.Servers != null
                    ? new EquatableDictionary<string, V3ServerDefinition>(_options.Servers)
                    : null,
                Channels = new EquatableDictionary<string, V3ChannelDefinition>(context.Channels),
                Operations = new EquatableDictionary<string, V3OperationDefinition>(context.Operations),
                Components = context.Messages.Count > 0
                    ? new V3ComponentDefinitionCollection
                    {
                        Messages = new EquatableDictionary<string, V3MessageDefinition>(context.Messages)
                    }
                    : null
            };

            return doc;
        }

        private async Task AddSubscriptionsAsync(
            GenerationContext context,
            CancellationToken ct)
        {
            if (_subscriptions == null) return;

            foreach (var subscription in _subscriptions)
            {
                if (subscription.RoutingKey == null || string.IsNullOrEmpty(subscription.RoutingKey.Value))
                    continue;

                await ProcessSourceAsync(
                    subscription.RoutingKey.Value, V3OperationAction.Receive, subscription.RequestType,
                    context, ct).ConfigureAwait(false);
            }
        }

        private async Task AddPublicationsAsync(
            GenerationContext context,
            CancellationToken ct)
        {
            if (_publications == null) return;

            foreach (var publication in _publications)
            {
                if (publication.Topic == null || string.IsNullOrEmpty(publication.Topic.Value))
                    continue;

                await ProcessSourceAsync(
                    publication.Topic.Value, V3OperationAction.Send, publication.RequestType,
                    context, ct).ConfigureAwait(false);
            }
        }

        private async Task ProcessSourceAsync(
            string address,
            V3OperationAction action,
            Type? requestType,
            GenerationContext context,
            CancellationToken ct)
        {
            var channelId = SanitizeChannelId(address);

            EnsureChannel(context.Channels, channelId, address);

            string messageName;
            if (requestType != null)
            {
                messageName = requestType.Name;
                await EnsureMessageAsync(context.Messages, messageName, requestType, ct).ConfigureAwait(false);
            }
            else
            {
                messageName = $"{channelId}Message";
                EnsurePlaceholderMessage(context.Messages, messageName);
            }

            AddChannelMessageRef(context.Channels, channelId, messageName);

            var actionString = action == V3OperationAction.Send ? "send" : "receive";
            context.CoveredChannelActions.Add((channelId, actionString));

            var operationId = GetUniqueOperationId(context.Operations, actionString, channelId);
            context.Operations[operationId] = new V3OperationDefinition
            {
                Action = action,
                Channel = new V3ReferenceDefinition { Reference = $"#/channels/{channelId}" },
                Messages = new EquatableList<V3ReferenceDefinition>
                {
                    new V3ReferenceDefinition { Reference = $"#/channels/{channelId}/messages/{messageName}" }
                }
            };
        }

        // codescene:ignore
        // Rationale: assembly scanning has unavoidable branching to preserve error handling,
        // deduplication, and null-safe reflection behavior without changing semantics.
        private async Task AddFromAssemblyScanningAsync(
            GenerationContext context,
            CancellationToken ct)
        {
            if (_options.DisableAssemblyScanning) return;

            var assemblies = _options.AssembliesToScan;
            if (assemblies == null)
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly == null) return;
                assemblies = new[] { entryAssembly };
            }

            foreach (var assembly in assemblies)
            {
                foreach (var (type, topic) in GetPublicationTopicTypes(assembly))
                {
                    var channelId = SanitizeChannelId(topic);

                    if (context.CoveredChannelActions.Contains((channelId, "send"))) continue;

                    EnsureChannel(context.Channels, channelId, topic);

                    var messageName = type.Name;
                    await EnsureMessageAsync(context.Messages, messageName, type, ct).ConfigureAwait(false);
                    AddChannelMessageRef(context.Channels, channelId, messageName);

                    var sendOpId = GetUniqueOperationId(context.Operations, "send", channelId);
                    context.Operations[sendOpId] = new V3OperationDefinition
                    {
                        Action = V3OperationAction.Send,
                        Channel = new V3ReferenceDefinition { Reference = $"#/channels/{channelId}" },
                        Messages = new EquatableList<V3ReferenceDefinition>
                        {
                            new V3ReferenceDefinition { Reference = $"#/channels/{channelId}/messages/{messageName}" }
                        }
                    };
                }
            }
        }

        private static IEnumerable<(Type type, string topic)> GetPublicationTopicTypes(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(IRequest).IsAssignableFrom(type)) continue;

                var attr = type.GetCustomAttribute<PublicationTopicAttribute>();
                if (attr == null) continue;

                var topic = attr.Destination?.RoutingKey?.Value;
                if (string.IsNullOrEmpty(topic)) continue;

                yield return (type, topic);
            }
        }

        private static void EnsureChannel(Dictionary<string, V3ChannelDefinition> channels, string channelId, string address)
        {
            channels.TryAdd(
                channelId,
                new V3ChannelDefinition
                {
                    Address = address,
                    Messages = new EquatableDictionary<string, V3MessageDefinition>()
                });
        }

        private async Task EnsureMessageAsync(Dictionary<string, V3MessageDefinition> messages, string messageName, Type requestType, CancellationToken ct)
        {
            if (!messages.TryGetValue(messageName, out _))
            {
                var schema = await _schemaGenerator.GenerateAsync(requestType, ct).ConfigureAwait(false)
                    ?? EmptyObjectSchema();

                var message = new V3MessageDefinition
                {
                    Name = messageName,
                    ContentType = "application/json",
                    Payload = RewriteEmbeddedSchemaRefs(schema, messageName)
                };

                messages.TryAdd(messageName, message);
            }
        }

        private static void EnsurePlaceholderMessage(Dictionary<string, V3MessageDefinition> messages, string messageName)
        {
            if (!messages.TryGetValue(messageName, out _))
            {
                using var emptyDoc = JsonDocument.Parse("{}");
                var message = new V3MessageDefinition
                {
                    Name = messageName,
                    ContentType = "application/json",
                    Payload = new V3SchemaDefinition
                    {
                        SchemaFormat = "application/schema+json;version=draft-07",
                        Schema = emptyDoc.RootElement.Clone()
                    }
                };

                messages.TryAdd(messageName, message);
            }
        }

        private static void AddChannelMessageRef(Dictionary<string, V3ChannelDefinition> channels, string channelId, string messageName)
        {
            if (!channels.TryGetValue(channelId, out var channel) || channel.Messages == null)
            {
                return;
            }

            channel.Messages.TryAdd(
                messageName,
                new V3MessageDefinition
                {
                    Reference = $"#/components/messages/{messageName}"
                });
        }

        private static string GetUniqueOperationId(Dictionary<string, V3OperationDefinition> operations, string action, string channelId)
        {
            var baseId = $"{action}_{channelId}";
            if (!operations.TryGetValue(baseId, out _))
                return baseId;

            var counter = 2;
            while (operations.TryGetValue($"{baseId}_{counter}", out _))
                counter++;

            return $"{baseId}_{counter}";
        }

        private static V3SchemaDefinition EmptyObjectSchema()
        {
            using var doc = JsonDocument.Parse("{}");
            return new V3SchemaDefinition
            {
                SchemaFormat = "application/schema+json;version=draft-07",
                Schema = doc.RootElement.Clone()
            };
        }

        private static readonly Regex s_sanitizeRegex = new("[^a-zA-Z0-9]", RegexOptions.Compiled);

        private static string SanitizeChannelId(string value) => s_sanitizeRegex.Replace(value, "_");

        private static V3SchemaDefinition? RewriteEmbeddedSchemaRefs(V3SchemaDefinition? schema, string messageName)
        {
            if (schema?.Schema is not JsonElement payload)
            {
                return schema;
            }

            var root = JsonNode.Parse(payload.GetRawText());
            if (root is null)
            {
                return schema;
            }

            var definitionsPrefix = $"#/components/messages/{messageName}/payload/definitions/";
            var defsPrefix = $"#/components/messages/{messageName}/payload/$defs/";
            RewriteRefs(root, definitionsPrefix, defsPrefix);

            using var rewritten = JsonDocument.Parse(root.ToJsonString());
            return new V3SchemaDefinition
            {
                SchemaFormat = schema.SchemaFormat,
                Schema = rewritten.RootElement.Clone()
            };
        }

        // codescene:ignore
        // Rationale: recursive JSON tree traversal is intentionally centralized here so
        // ref-rewriting stays consistent for objects and arrays across all schema shapes.
        private static void RewriteRefs(JsonNode node, string definitionsPrefix, string defsPrefix)
        {
            if (node is JsonObject obj)
            {
                RewriteRefProperty(obj, definitionsPrefix, defsPrefix);

                foreach (var property in obj)
                {
                    if (property.Value != null)
                    {
                        RewriteRefs(property.Value, definitionsPrefix, defsPrefix);
                    }
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    if (item != null)
                    {
                        RewriteRefs(item, definitionsPrefix, defsPrefix);
                    }
                }
            }
        }

        private static void RewriteRefProperty(JsonObject obj, string definitionsPrefix, string defsPrefix)
        {
            if (!obj.TryGetPropertyValue("$ref", out var refNode) ||
                refNode is not JsonValue refValue ||
                !refValue.TryGetValue<string>(out var refString))
            {
                return;
            }

            if (refString.StartsWith("#/definitions/"))
            {
                obj["$ref"] = $"{definitionsPrefix}{refString.Substring("#/definitions/".Length)}";
            }
            else if (refString.StartsWith("#/$defs/"))
            {
                obj["$ref"] = $"{defsPrefix}{refString.Substring("#/$defs/".Length)}";
            }
        }
    }
}
