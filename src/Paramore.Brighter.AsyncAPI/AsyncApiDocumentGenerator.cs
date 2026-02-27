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
    public class AsyncApiDocumentGenerator : IAmAnAsyncApiDocumentGenerator
    {
        private readonly AsyncApiOptions _options;
        private readonly IAmASchemaGenerator _schemaGenerator;
        private readonly IEnumerable<Subscription>? _subscriptions;
        private readonly IEnumerable<Publication>? _publications;

        private readonly Dictionary<string, AsyncApiChannel> _channels = new();
        private readonly Dictionary<string, AsyncApiOperation> _operations = new();
        private readonly Dictionary<string, AsyncApiMessage> _messages = new();
        private readonly HashSet<(string channelId, string action)> _coveredChannelActions = new();

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
            _channels.Clear();
            _operations.Clear();
            _messages.Clear();
            _coveredChannelActions.Clear();

            await AddSubscriptionsAsync(ct).ConfigureAwait(false);
            await AddPublicationsAsync(ct).ConfigureAwait(false);
            await AddFromAssemblyScanningAsync(ct).ConfigureAwait(false);

            var doc = new AsyncApiDocument
            {
                Info = new AsyncApiInfo
                {
                    Title = _options.Title,
                    Version = _options.Version,
                    Description = _options.Description
                },
                Servers = _options.Servers,
                Channels = _channels.Count > 0 ? _channels : null,
                Operations = _operations.Count > 0 ? _operations : null,
                Components = _messages.Count > 0 ? new AsyncApiComponents { Messages = _messages } : null
            };

            return doc;
        }

        private async Task AddSubscriptionsAsync(CancellationToken ct)
        {
            if (_subscriptions == null) return;

            foreach (var subscription in _subscriptions)
            {
                if (subscription.RoutingKey == null || string.IsNullOrEmpty(subscription.RoutingKey.Value))
                    continue;

                var channelId = SanitizeChannelId(subscription.RoutingKey.Value);
                var address = subscription.RoutingKey.Value;

                EnsureChannel(channelId, address);

                string messageName;
                if (subscription.RequestType != null)
                {
                    messageName = subscription.RequestType.Name;
                    await EnsureMessageAsync(messageName, subscription.RequestType, ct).ConfigureAwait(false);
                }
                else
                {
                    messageName = $"{channelId}Message";
                    EnsurePlaceholderMessage(messageName);
                }

                AddChannelMessageRef(channelId, messageName);

                _coveredChannelActions.Add((channelId, "receive"));

                var operationId = GetUniqueOperationId("receive", channelId);
                _operations[operationId] = new AsyncApiOperation
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

        private async Task AddPublicationsAsync(CancellationToken ct)
        {
            if (_publications == null) return;

            foreach (var publication in _publications)
            {
                if (publication.Topic == null || string.IsNullOrEmpty(publication.Topic.Value))
                    continue;

                var channelId = SanitizeChannelId(publication.Topic.Value);
                var address = publication.Topic.Value;

                EnsureChannel(channelId, address);

                string messageName;
                if (publication.RequestType != null)
                {
                    messageName = publication.RequestType.Name;
                    await EnsureMessageAsync(messageName, publication.RequestType, ct).ConfigureAwait(false);
                }
                else
                {
                    messageName = $"{channelId}Message";
                    EnsurePlaceholderMessage(messageName);
                }

                AddChannelMessageRef(channelId, messageName);

                _coveredChannelActions.Add((channelId, "send"));

                var operationId = GetUniqueOperationId("send", channelId);
                _operations[operationId] = new AsyncApiOperation
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

        private async Task AddFromAssemblyScanningAsync(CancellationToken ct)
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
                    if (_coveredChannelActions.Contains((channelId, "send"))) continue;

                    var sendOpId = $"send_{channelId}";

                    EnsureChannel(channelId, topic);

                    var messageName = type.Name;
                    await EnsureMessageAsync(messageName, type, ct).ConfigureAwait(false);
                    AddChannelMessageRef(channelId, messageName);

                    _operations[sendOpId] = new AsyncApiOperation
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

        private void EnsureChannel(string channelId, string address)
        {
            if (!_channels.ContainsKey(channelId))
            {
                _channels[channelId] = new AsyncApiChannel
                {
                    Address = address,
                    Messages = new Dictionary<string, AsyncApiRef>()
                };
            }
        }

        private async Task EnsureMessageAsync(string messageName, Type requestType, CancellationToken ct)
        {
            if (!_messages.ContainsKey(messageName))
            {
                _messages[messageName] = new AsyncApiMessage
                {
                    Name = messageName,
                    ContentType = "application/json",
                    Payload = await _schemaGenerator.GenerateAsync(requestType, ct).ConfigureAwait(false)
                };
            }
        }

        private void EnsurePlaceholderMessage(string messageName)
        {
            if (!_messages.ContainsKey(messageName))
            {
                using var emptyDoc = JsonDocument.Parse("{}");
                _messages[messageName] = new AsyncApiMessage
                {
                    Name = messageName,
                    ContentType = "application/json",
                    Payload = emptyDoc.RootElement.Clone()
                };
            }
        }

        private void AddChannelMessageRef(string channelId, string messageName)
        {
            var channel = _channels[channelId];
            if (channel.Messages != null && !channel.Messages.ContainsKey(messageName))
            {
                channel.Messages[messageName] = new AsyncApiRef { Ref = $"#/components/messages/{messageName}" };
            }
        }

        private string GetUniqueOperationId(string action, string channelId)
        {
            var baseId = $"{action}_{channelId}";
            if (!_operations.ContainsKey(baseId))
                return baseId;

            var counter = 2;
            while (_operations.ContainsKey($"{baseId}_{counter}"))
                counter++;

            return $"{baseId}_{counter}";
        }

        private static string SanitizeChannelId(string value)
        {
            return Regex.Replace(value, "[^a-zA-Z0-9]", "_");
        }
    }
}
