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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.AsyncAPI.Model;

namespace Paramore.Brighter.AsyncAPI
{
    public sealed class AsyncApiDocumentGenerator : IAmAnAsyncApiDocumentGenerator
    {
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

        public async Task<AsyncApiDocument> GenerateAsync(CancellationToken ct = default)
        {
            var channels = new Dictionary<string, AsyncApiChannel>();
            var operations = new Dictionary<string, AsyncApiOperation>();
            var messages = new Dictionary<string, AsyncApiMessage>();
            var coveredChannelActions = new HashSet<(string channelId, string action)>();

            await AddSubscriptionsAsync(channels, operations, messages, coveredChannelActions, ct).ConfigureAwait(false);
            await AddPublicationsAsync(channels, operations, messages, coveredChannelActions, ct).ConfigureAwait(false);
            await AddFromAssemblyScanningAsync(channels, operations, messages, coveredChannelActions, ct).ConfigureAwait(false);

            var doc = new AsyncApiDocument
            {
                Info = new AsyncApiInfo
                {
                    Title = _options.Title,
                    Version = _options.Version,
                    Description = _options.Description
                },
                Servers = _options.Servers != null ? new Dictionary<string, AsyncApiServer>(_options.Servers) : null,
                Channels = channels.Count > 0 ? channels : null,
                Operations = operations.Count > 0 ? operations : null,
                Components = messages.Count > 0 ? new AsyncApiComponents { Messages = messages } : null
            };

            return doc;
        }

        private async Task AddSubscriptionsAsync(
            Dictionary<string, AsyncApiChannel> channels,
            Dictionary<string, AsyncApiOperation> operations,
            Dictionary<string, AsyncApiMessage> messages,
            HashSet<(string channelId, string action)> coveredChannelActions,
            CancellationToken ct)
        {
            if (_subscriptions == null) return;

            foreach (var subscription in _subscriptions)
            {
                if (subscription.RoutingKey == null || string.IsNullOrEmpty(subscription.RoutingKey.Value))
                    continue;

                var channelId = SanitizeChannelId(subscription.RoutingKey.Value);
                var address = subscription.RoutingKey.Value;

                EnsureChannel(channels, channelId, address);

                string messageName;
                if (subscription.RequestType != null)
                {
                    messageName = subscription.RequestType.Name;
                    await EnsureMessageAsync(messages, messageName, subscription.RequestType, ct).ConfigureAwait(false);
                }
                else
                {
                    messageName = $"{channelId}Message";
                    EnsurePlaceholderMessage(messages, messageName);
                }

                AddChannelMessageRef(channels, channelId, messageName);

                coveredChannelActions.Add((channelId, "receive"));

                var operationId = GetUniqueOperationId(operations, "receive", channelId);
                operations[operationId] = new AsyncApiOperation
                {
                    Action = "receive",
                    Channel = new AsyncApiRef { Ref = $"#/channels/{channelId}" },
                    Messages = new List<AsyncApiRef>
                    {
                        new AsyncApiRef { Ref = $"#/channels/{channelId}/messages/{messageName}" }
                    }
                };
            }
        }

        private async Task AddPublicationsAsync(
            Dictionary<string, AsyncApiChannel> channels,
            Dictionary<string, AsyncApiOperation> operations,
            Dictionary<string, AsyncApiMessage> messages,
            HashSet<(string channelId, string action)> coveredChannelActions,
            CancellationToken ct)
        {
            if (_publications == null) return;

            foreach (var publication in _publications)
            {
                if (publication.Topic == null || string.IsNullOrEmpty(publication.Topic.Value))
                    continue;

                var channelId = SanitizeChannelId(publication.Topic.Value);
                var address = publication.Topic.Value;

                EnsureChannel(channels, channelId, address);

                string messageName;
                if (publication.RequestType != null)
                {
                    messageName = publication.RequestType.Name;
                    await EnsureMessageAsync(messages, messageName, publication.RequestType, ct).ConfigureAwait(false);
                }
                else
                {
                    messageName = $"{channelId}Message";
                    EnsurePlaceholderMessage(messages, messageName);
                }

                AddChannelMessageRef(channels, channelId, messageName);

                coveredChannelActions.Add((channelId, "send"));

                var operationId = GetUniqueOperationId(operations, "send", channelId);
                operations[operationId] = new AsyncApiOperation
                {
                    Action = "send",
                    Channel = new AsyncApiRef { Ref = $"#/channels/{channelId}" },
                    Messages = new List<AsyncApiRef>
                    {
                        new AsyncApiRef { Ref = $"#/channels/{channelId}/messages/{messageName}" }
                    }
                };
            }
        }

        private async Task AddFromAssemblyScanningAsync(
            Dictionary<string, AsyncApiChannel> channels,
            Dictionary<string, AsyncApiOperation> operations,
            Dictionary<string, AsyncApiMessage> messages,
            HashSet<(string channelId, string action)> coveredChannelActions,
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

                    var channelId = SanitizeChannelId(topic);

                    // DI sources win deduplication
                    if (coveredChannelActions.Contains((channelId, "send"))) continue;

                    var sendOpId = $"send_{channelId}";

                    EnsureChannel(channels, channelId, topic);

                    var messageName = type.Name;
                    await EnsureMessageAsync(messages, messageName, type, ct).ConfigureAwait(false);
                    AddChannelMessageRef(channels, channelId, messageName);

                    operations[sendOpId] = new AsyncApiOperation
                    {
                        Action = "send",
                        Channel = new AsyncApiRef { Ref = $"#/channels/{channelId}" },
                        Messages = new List<AsyncApiRef>
                        {
                            new AsyncApiRef { Ref = $"#/channels/{channelId}/messages/{messageName}" }
                        }
                    };
                }
            }
        }

        private static void EnsureChannel(Dictionary<string, AsyncApiChannel> channels, string channelId, string address)
        {
            channels.TryAdd(
                channelId,
                new AsyncApiChannel
                {
                    Address = address,
                    Messages = new Dictionary<string, AsyncApiRef>()
                });
        }

        private async Task EnsureMessageAsync(Dictionary<string, AsyncApiMessage> messages, string messageName, Type requestType, CancellationToken ct)
        {
            if (!messages.TryGetValue(messageName, out _))
            {
                var message = new AsyncApiMessage
                {
                    Name = messageName,
                    ContentType = "application/json",
                    Payload = await _schemaGenerator.GenerateAsync(requestType, ct).ConfigureAwait(false)
                };

                messages.TryAdd(messageName, message);
            }
        }

        private static void EnsurePlaceholderMessage(Dictionary<string, AsyncApiMessage> messages, string messageName)
        {
            if (!messages.TryGetValue(messageName, out _))
            {
                using var emptyDoc = JsonDocument.Parse("{}");
                var message = new AsyncApiMessage
                {
                    Name = messageName,
                    ContentType = "application/json",
                    Payload = emptyDoc.RootElement.Clone()
                };

                messages.TryAdd(messageName, message);
            }
        }

        private static void AddChannelMessageRef(Dictionary<string, AsyncApiChannel> channels, string channelId, string messageName)
        {
            if (!channels.TryGetValue(channelId, out var channel) || channel.Messages == null)
            {
                return;
            }

            channel.Messages.TryAdd(
                messageName,
                new AsyncApiRef
                {
                    Ref = $"#/components/messages/{messageName}"
                });
        }

        private static string GetUniqueOperationId(Dictionary<string, AsyncApiOperation> operations, string action, string channelId)
        {
            var baseId = $"{action}_{channelId}";
            if (!operations.TryGetValue(baseId, out _))
                return baseId;

            var counter = 2;
            while (operations.TryGetValue($"{baseId}_{counter}", out _))
                counter++;

            return $"{baseId}_{counter}";
        }

        private static readonly Regex s_sanitizeRegex = new("[^a-zA-Z0-9]", RegexOptions.Compiled);

        private static string SanitizeChannelId(string value) => s_sanitizeRegex.Replace(value, "_");
    }
}
