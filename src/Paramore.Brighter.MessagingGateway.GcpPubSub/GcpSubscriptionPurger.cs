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

using System.Diagnostics;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using GcpSubscriptionName = Google.Cloud.PubSub.V1.SubscriptionName;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

internal static partial class GcpSubscriptionPurger
{
    private static readonly ILogger s_logger = ApplicationLogging.LoggerFactory.CreateLogger(typeof(GcpSubscriptionPurger));
    private static readonly CallSettings s_cleanupSettings = CallSettings
        .FromExpiration(Expiration.FromTimeout(TimeSpan.FromSeconds(10)))
        .WithRetry(RetrySettings.FromExponentialBackoff(3, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1), 2,
            RetrySettings.FilterForStatusCodes(StatusCode.Unavailable)));
    private static readonly CallSettings s_subscriptionSettings = CallSettings.FromRetry(
        RetrySettings.FromExponentialBackoff(10, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2), 2,
            RetrySettings.FilterForStatusCodes(StatusCode.NotFound, StatusCode.Unavailable)));

    public static Timestamp Purge(SubscriberServiceApiClient client, PublisherServiceApiClient publisher,
        GcpSubscriptionName subscriptionName)
    {
        var resourceId = $"brighter-purge-{Guid.NewGuid():N}";
        var temporarySubscription = GcpSubscriptionName.FromProjectSubscription(subscriptionName.ProjectId, resourceId);
        var temporaryTopic = TopicName.FromProjectTopic(subscriptionName.ProjectId, resourceId);
        var subscriptionAttempted = false;
        Exception? failure = null;
        Timestamp purgeBefore;

        try
        {
            publisher.CreateTopic(temporaryTopic);
            subscriptionAttempted = true;
            client.CreateSubscription(CreateTemporarySubscription(temporarySubscription, temporaryTopic), s_subscriptionSettings);
            var markerId = publisher.Publish(temporaryTopic, new[] { CreateMarker() }).MessageIds.Single();
            purgeBefore = ReceiveMarker(client, temporarySubscription, markerId);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            var cleanupFailures = new List<Exception>();
            if (subscriptionAttempted)
            {
                DeleteResource(() => client.DeleteSubscription(temporarySubscription, s_cleanupSettings),
                    temporarySubscription.ToString(), cleanupFailures);
            }
            DeleteResource(() => publisher.DeleteTopic(temporaryTopic, s_cleanupSettings), temporaryTopic.ToString(), cleanupFailures);
            HandleCleanupFailures(failure, cleanupFailures);
        }

        client.Seek(new SeekRequest
        {
            SubscriptionAsSubscriptionName = subscriptionName,
            Time = purgeBefore
        });
        return purgeBefore;
    }

    public static async Task<Timestamp> PurgeAsync(SubscriberServiceApiClient client, PublisherServiceApiClient publisher,
        GcpSubscriptionName subscriptionName, CancellationToken cancellationToken)
    {
        var resourceId = $"brighter-purge-{Guid.NewGuid():N}";
        var temporarySubscription = GcpSubscriptionName.FromProjectSubscription(subscriptionName.ProjectId, resourceId);
        var temporaryTopic = TopicName.FromProjectTopic(subscriptionName.ProjectId, resourceId);
        var subscriptionAttempted = false;
        Exception? failure = null;
        Timestamp purgeBefore;

        try
        {
            await publisher.CreateTopicAsync(temporaryTopic, cancellationToken);
            subscriptionAttempted = true;
            await client.CreateSubscriptionAsync(CreateTemporarySubscription(temporarySubscription, temporaryTopic),
                s_subscriptionSettings.WithCancellationToken(cancellationToken));
            var publication = await publisher.PublishAsync(temporaryTopic, new[] { CreateMarker() }, cancellationToken);
            purgeBefore = await ReceiveMarkerAsync(client, temporarySubscription, publication.MessageIds.Single(), cancellationToken);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            var cleanupFailures = new List<Exception>();
            if (subscriptionAttempted)
            {
                await DeleteResourceAsync(() => client.DeleteSubscriptionAsync(temporarySubscription, s_cleanupSettings),
                    temporarySubscription.ToString(), cleanupFailures);
            }
            await DeleteResourceAsync(() => publisher.DeleteTopicAsync(temporaryTopic, s_cleanupSettings),
                temporaryTopic.ToString(), cleanupFailures);
            HandleCleanupFailures(failure, cleanupFailures);
        }

        await client.SeekAsync(new SeekRequest
        {
            SubscriptionAsSubscriptionName = subscriptionName,
            Time = purgeBefore
        }, cancellationToken);
        return purgeBefore;
    }

    private static void DeleteResource(Action delete, string resourceName, List<Exception> failures)
    {
        try
        {
            delete();
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
        }
        catch (Exception exception)
        {
            failures.Add(exception);
            LogCleanupFailure(s_logger, exception, resourceName);
        }
    }

    private static async Task DeleteResourceAsync(Func<Task> delete, string resourceName, List<Exception> failures)
    {
        try
        {
            await delete();
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
        }
        catch (Exception exception)
        {
            failures.Add(exception);
            LogCleanupFailure(s_logger, exception, resourceName);
        }
    }

    private static void HandleCleanupFailures(Exception? failure, List<Exception> cleanupFailures)
    {
        if (cleanupFailures.Count == 0)
            return;

        var cleanupException = new AggregateException("Could not remove temporary Pub/Sub purge resources.", cleanupFailures);
        if (failure == null)
            throw cleanupException;
        failure.Data["PubSubPurgeCleanupException"] = cleanupException;
    }

    [LoggerMessage(LogLevel.Error, "Could not remove temporary Pub/Sub purge resource {ResourceName}")]
    private static partial void LogCleanupFailure(ILogger logger, Exception exception, string resourceName);

    private static Google.Cloud.PubSub.V1.Subscription CreateTemporarySubscription(GcpSubscriptionName name, TopicName topic) => new()
    {
        SubscriptionName = name,
        TopicAsTopicName = topic,
        AckDeadlineSeconds = 10,
        MessageRetentionDuration = Duration.FromTimeSpan(TimeSpan.FromMinutes(10)),
        ExpirationPolicy = new ExpirationPolicy { Ttl = Duration.FromTimeSpan(TimeSpan.FromDays(1)) }
    };

    private static PubsubMessage CreateMarker() => new() { Data = ByteString.CopyFromUtf8("brighter-purge") };

    private static Timestamp ReceiveMarker(SubscriberServiceApiClient client, GcpSubscriptionName subscription, string markerId)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            var response = client.Pull(new PullRequest
            {
                SubscriptionAsSubscriptionName = subscription,
                MaxMessages = 1
            }, MarkerCallSettings(elapsed));
            var boundary = FindMarker(response, markerId);
            if (boundary != null)
                return boundary;
        }
    }

    private static async Task<Timestamp> ReceiveMarkerAsync(SubscriberServiceApiClient client,
        GcpSubscriptionName subscription, string markerId, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            var response = await client.PullAsync(new PullRequest
            {
                SubscriptionAsSubscriptionName = subscription,
                MaxMessages = 1
            }, MarkerCallSettings(elapsed).WithCancellationToken(cancellationToken));
            var boundary = FindMarker(response, markerId);
            if (boundary != null)
                return boundary;
        }
    }

    private static CallSettings MarkerCallSettings(Stopwatch elapsed)
    {
        var remaining = TimeSpan.FromSeconds(30) - elapsed.Elapsed;
        if (remaining <= TimeSpan.Zero)
            throw new TimeoutException("Timed out receiving the Pub/Sub purge marker.");
        return CallSettings.FromExpiration(Expiration.FromTimeout(remaining));
    }

    private static Timestamp? FindMarker(PullResponse response, string markerId)
    {
        var marker = response.ReceivedMessages.FirstOrDefault(message => message.Message.MessageId == markerId)?.Message;
        if (marker == null)
            return null;
        return marker.PublishTime?.Clone()
            ?? throw new InvalidOperationException("Pub/Sub did not return a publication time for the purge marker.");
    }
}
