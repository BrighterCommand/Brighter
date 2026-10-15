#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia

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

#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.TestDoubles;

internal sealed class GcpPurgeTestContext : IDisposable
{
    private readonly GrpcChannel _transport;
    private readonly GcpPurgeFaultInterceptor _faults;
    private readonly PublisherServiceApiClient _topics;
    private readonly IAmAChannelSync? _syncChannel;
    private readonly IAmAChannelSync? _peerSyncChannel;
    private readonly IAmAChannelAsync? _asyncChannel;
    private readonly IAmAChannelAsync? _peerAsyncChannel;
    private readonly string _project;

    public GcpPurgeTestContext(SubscriptionMode mode, GcpPurgeFaultInterceptor faults, bool useAsync)
    {
        var host = Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST")
            ?? throw new InvalidOperationException("These tests require PUBSUB_EMULATOR_HOST.");
        _project = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "brighter-test";
        _faults = faults;
        _transport = GrpcChannel.ForAddress("http://" + host,
            new GrpcChannelOptions { Credentials = ChannelCredentials.Insecure });
        Admin = new SubscriberServiceApiClientBuilder { CallInvoker = _transport.CreateCallInvoker() }.Build();
        _topics = new PublisherServiceApiClientBuilder { CallInvoker = _transport.CreateCallInvoker() }.Build();
        var connection = new GcpMessagingGatewayConnection
        {
            ProjectId = _project,
            TopicManagerConfiguration = builder => builder.CallInvoker = _transport.Intercept(faults),
            StreamConfiguration = builder => builder.EmulatorDetection = EmulatorDetection.EmulatorOnly,
            SubscriptionManagerConfiguration = builder => builder.CallInvoker = _transport.Intercept(faults)
        };
        var id = "purge-review-" + Guid.NewGuid().ToString("N");
        Topic = TopicName.FromProjectTopic(_project, id);
        var subscription = new GcpPubSubSubscription<MyCommand>(new SubscriptionName(id),
            new ChannelName(id), new RoutingKey(id), subscriptionMode: mode, messagePumpType: useAsync ? MessagePumpType.Proactor : MessagePumpType.Reactor, makeChannels: OnMissingChannel.Create);
        var factory = new GcpPubSubChannelFactory(connection);
        if (useAsync)
        {
            _asyncChannel = factory.CreateAsyncChannel(subscription);
            _peerAsyncChannel = factory.CreateAsyncChannel(subscription);
        }
        else
        {
            _syncChannel = factory.CreateSyncChannel(subscription);
            _peerSyncChannel = factory.CreateSyncChannel(subscription);
        }
    }

    public SubscriberServiceApiClient Admin { get; }
    public TopicName Topic { get; }

    public Task PurgeAsync(bool useAsync, CancellationToken cancellationToken = default, bool peer = false) => useAsync
        ? (peer ? _peerAsyncChannel! : _asyncChannel!).PurgeAsync(cancellationToken)
        : Task.Run((peer ? _peerSyncChannel! : _syncChannel!).Purge, cancellationToken);

    public string[] TemporarySubscriptions() => Admin.ListSubscriptions("projects/" + _project)
        .Where(subscription => _faults.AttemptedSubscriptions.ContainsKey(subscription.Name) && subscription.Name.Contains("/brighter-purge-"))
        .Select(subscription => subscription.Name).ToArray();

    public string[] TemporaryTopics() => _topics.ListTopics("projects/" + _project)
        .Where(topic => _faults.AttemptedTopics.ContainsKey(topic.Name) && topic.Name.Contains("/brighter-purge-"))
        .Select(topic => topic.Name).ToArray();

    public void Dispose()
    {
        _faults.BeforeCall = null;
        _faults.AfterCall = null;
        _syncChannel?.Dispose();
        _peerSyncChannel?.Dispose();
        _asyncChannel?.Dispose();
        _peerAsyncChannel?.Dispose();
        foreach (var subscription in TemporarySubscriptions())
        {
            Admin.DeleteSubscription(subscription);
        }
        foreach (var topic in TemporaryTopics())
        {
            _topics.DeleteTopic(topic);
        }
        foreach (var subscription in Admin.ListSubscriptions("projects/" + _project)
                     .Where(subscription => subscription.Topic == Topic.ToString()).ToArray())
        {
            Admin.DeleteSubscription(subscription.Name);
        }
        _topics.DeleteTopic(Topic);
        _transport.Dispose();
    }
}
