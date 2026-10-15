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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

[Trait("Category", "GcpPubSubPurge")]
public class GcpPurgeConcurrentPublicationTests
{
    [Theory]
    [InlineData(false, SubscriptionMode.Pull)]
    [InlineData(true, SubscriptionMode.Pull)]
    [InlineData(false, SubscriptionMode.Stream)]
    [InlineData(true, SubscriptionMode.Stream)]
    public async Task When_purging_during_publication_should_discard_the_old_backlog_and_deliver_new_messages(
        bool useAsync, SubscriptionMode mode)
    {
        //Arrange
        var project = GatewayFactory.GetProjectId();
        var credential = GatewayFactory.GetCredential();
        var topics = new PublisherServiceApiClientBuilder
        {
            Credential = credential,
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.Build();
        var admin = new SubscriberServiceApiClientBuilder
        {
            Credential = credential,
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.Build();
        var id = "purge-concurrent-" + Guid.NewGuid().ToString("N");
        var topic = TopicName.FromProjectTopic(project, id);
        var source = Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(project, id);
        var connection = new GcpMessagingGatewayConnection
        {
            ProjectId = project,
            Credential = credential,
            TopicManagerConfiguration = builder => builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
            SubscriptionManagerConfiguration = builder => builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
            StreamConfiguration = builder => builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        };
        var subscription = new GcpPubSubSubscription<MyCommand>(new SubscriptionName(id), new ChannelName(id),
            new RoutingKey(id), makeChannels: OnMissingChannel.Assume, subscriptionMode: mode,
            messagePumpType: useAsync ? MessagePumpType.Proactor : MessagePumpType.Reactor,
            timeProvider: new LaggingTimeProvider(TimeSpan.FromMinutes(5)));
        var factory = new GcpPubSubChannelFactory(connection);
        IAmAChannelSync? syncChannel = null;
        IAmAChannelAsync? asyncChannel = null;
        using var stopPublishing = new CancellationTokenSource();
        Task? publishing = null;
        var published = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await topics.CreateTopicAsync(new Topic
            {
                TopicName = topic,
                MessageRetentionDuration = Duration.FromTimeSpan(TimeSpan.FromHours(1))
            });
            await admin.CreateSubscriptionAsync(source, topic, null, 10);
            for (var i = 0; i < 20; i++)
                await PublishAsync("old");
            if (useAsync)
                asyncChannel = await factory.CreateAsyncChannelAsync(subscription);
            else
                syncChannel = factory.CreateSyncChannel(subscription);
            publishing = Task.Run(async () =>
            {
                while (!stopPublishing.IsCancellationRequested)
                {
                    await PublishAsync("concurrent");
                    published.TrySetResult(true);
                    await Task.Delay(10);
                }
            });
            await Task.WhenAny(published.Task, publishing).WaitAsync(TimeSpan.FromSeconds(20));
            if (publishing.IsCompleted)
                await publishing;

            //Act
            if (useAsync)
                await asyncChannel!.PurgeAsync();
            else
                syncChannel!.Purge();
            stopPublishing.Cancel();
            await publishing;
            await PublishAsync("after-purge");

            //Assert
            var elapsed = Stopwatch.StartNew();
            var foundNewMessage = false;
            while (elapsed.Elapsed < TimeSpan.FromSeconds(90) && !foundNewMessage)
            {
                var message = useAsync
                    ? await asyncChannel!.ReceiveAsync(TimeSpan.FromSeconds(1))
                    : syncChannel!.Receive(TimeSpan.FromSeconds(1));
                if (message.IsEmpty)
                    continue;
                Assert.NotEqual("old", message.Body.Value);
                foundNewMessage = message.Body.Value == "after-purge";
                if (useAsync)
                    await asyncChannel!.AcknowledgeAsync(message);
                else
                    syncChannel!.Acknowledge(message);
            }
            Assert.True(foundNewMessage, "A message published after purge completion must remain deliverable.");
        }
        finally
        {
            stopPublishing.Cancel();
            if (publishing != null)
                await publishing.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
            try
            {
                syncChannel?.Dispose();
                if (asyncChannel != null)
                    await asyncChannel.DisposeAsync();
            }
            finally
            {
                try
                { await DeleteResourceAsync(() => admin.DeleteSubscriptionAsync(source)); }
                finally { await DeleteResourceAsync(() => topics.DeleteTopicAsync(topic)); }
            }
        }

        Task PublishAsync(string body) => topics.PublishAsync(topic, new[] { new PubsubMessage { Data = ByteString.CopyFromUtf8(body) } });
    }

    private static async Task DeleteResourceAsync(Func<Task> delete)
    {
        try
        { await delete(); }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound) { }
    }
}
