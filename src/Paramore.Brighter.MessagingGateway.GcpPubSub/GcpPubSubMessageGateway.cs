using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Pub/Sub message gateway
/// </summary>
/// <param name="connection">The <see cref="GcpMessagingGatewayConnection"/>.</param>
public class GcpPubSubMessageGateway(GcpMessagingGatewayConnection connection)
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<GcpPubSubMessageGateway>();

    /// <summary>
    /// The <see cref="GcpMessagingGatewayConnection"/>
    /// </summary>
    protected GcpMessagingGatewayConnection Connection { get; } = connection;

    internal async Task EnsureTopicExistAsync(TopicAttributes publication, OnMissingChannel makeChannel)
    {
        var topicName = GetTopicName(publication.ProjectId, publication.Name);
        if (makeChannel == OnMissingChannel.Assume)
        {
            return;
        }

        var publisher = await Connection.CreatePublisherServiceApiClientAsync();
        if (makeChannel == OnMissingChannel.Validate)
        {
            if (await DoesTopicExistAsync(publisher, topicName))
            {
                return;
            }

            // TODO: Change exception
            throw new(
                $"Topic validation error: could not find topic {topicName}. Did you want Brighter to create infrastructure?");
        }

        if (await DoesTopicExistAsync(publisher, topicName))
        {
            await UpdateTopicAsync(publisher, topicName, publication);
        }
        else
        {
            await CreateTopicAsync(publisher, topicName, publication);
        }

        return;

        static async Task CreateTopicAsync(PublisherServiceApiClient publisher, TopicName topicName,
            TopicAttributes publication)
        {
            await publisher.CreateTopicAsync(CreateTopic(topicName, publication));
        }

        static async Task UpdateTopicAsync(PublisherServiceApiClient publisher, TopicName topicName,
            TopicAttributes publication)
        {
            var mask = new FieldMask();
            mask.Paths.Add("message_store_policy");
            mask.Paths.Add("message_retention_duration");
            mask.Paths.Add("kms_key_name");
            mask.Paths.Add("schema_settings");
            mask.Paths.Add("label");
            mask.Paths.Add("state");

            var topic = new UpdateTopicRequest { Topic = CreateTopic(topicName, publication), UpdateMask = mask };
            await publisher.UpdateTopicAsync(topic);
        }

        static Topic CreateTopic(TopicName topicName, TopicAttributes publication)
        {
            var topic = new Topic { TopicName = topicName, State = Topic.Types.State.Active };

            if (publication.StorePolicy != null)
            {
                topic.MessageStoragePolicy = publication.StorePolicy;
            }

            if (publication.MessageRetentionDuration != null)
            {
                topic.MessageRetentionDuration = Duration.FromTimeSpan(publication.MessageRetentionDuration.Value);
            }

            if (!string.IsNullOrEmpty(publication.KmsKeyName))
            {
                topic.KmsKeyName = publication.KmsKeyName;
            }

            if (publication.SchemaSettings != null)
            {
                topic.SchemaSettings = publication.SchemaSettings;
            }

            topic.Labels.Add("Source", "Brighter");

            foreach (var label in publication.Labels)
            {
                topic.Labels[label.Key] = label.Value;
            }

            return topic;
        }
    }

    private static async Task<bool> DoesTopicExistAsync(PublisherServiceApiClient client, TopicName topicName)
    {
        try
        {
            var topic = await client.GetTopicAsync(topicName);
            return topic != null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return false;
        }
    }

    internal async Task EnsureSubscriptionExistsAsync(GcpSubscription subscription)
    {
        if (subscription.MakeChannels == OnMissingChannel.Assume)
        {
            return;
        }

        subscription.TopicAttributes ??= new TopicAttributes();
        if (string.IsNullOrEmpty(subscription.TopicAttributes.Name))
        {
            subscription.TopicAttributes.Name = subscription.RoutingKey;
        }

        await EnsureTopicExistAsync(subscription.TopicAttributes, subscription.MakeChannels);

        if (!Google.Cloud.PubSub.V1.SubscriptionName.TryParse(subscription.ChannelName.Value, out var subscriptionName))
        {
            subscriptionName = GetSubscriptionName(subscription.ProjectId, subscription.ChannelName.Value);
        }

        var client = await Connection.CreateSubscriberServiceApiClientAsync();
        if (subscription.MakeChannels == OnMissingChannel.Validate)
        {
            if (await DoesSubscriptionExists(client, subscriptionName))
            {
                return;
            }

            // TODO: Update exception type
            throw new Exception("No subscription found");
        }

        if (await DoesSubscriptionExists(client, subscriptionName))
        {
            await UpdateSubscriptionAsync(client,
                Connection.ProjectId,
                subscriptionName,
                subscription);
        }
        else
        {
            await CreateSubscriptionAsync(client,
                Connection.ProjectId,
                subscriptionName,
                subscription);
        }

        return;

        static async Task CreateSubscriptionAsync(SubscriberServiceApiClient client,
            string projectId,
            Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
            GcpSubscription subscription)
        {
            await client.CreateSubscriptionAsync(CreateSubscription(projectId, subscriptionName, subscription));
        }

        static async Task UpdateSubscriptionAsync(SubscriberServiceApiClient client,
            string projectId,
            Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
            GcpSubscription subscription)
        {
            var request = new UpdateSubscriptionRequest
            {
                Subscription = CreateSubscription(projectId, subscriptionName, subscription),
                UpdateMask = new FieldMask()
            };

            request.UpdateMask.Paths.Add("ack_deadline_seconds");
            request.UpdateMask.Paths.Add("retain_acked_messages");
            request.UpdateMask.Paths.Add("enable_message_ordering");
            request.UpdateMask.Paths.Add("enable_exactly_once_delivery");
            request.UpdateMask.Paths.Add("state");
            request.UpdateMask.Paths.Add("message_retention_duration");
            request.UpdateMask.Paths.Add("expiration_policy");
            request.UpdateMask.Paths.Add("dead_letter_topic");
            request.UpdateMask.Paths.Add("dead_letter_policy");
            request.UpdateMask.Paths.Add("retry_policy");
            request.UpdateMask.Paths.Add("labels");

            await client.UpdateSubscriptionAsync(request);
        }

        static Google.Cloud.PubSub.V1.Subscription CreateSubscription(string projectId,
            Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
            GcpSubscription subscription)
        {
            var gpcSubscription = new Google.Cloud.PubSub.V1.Subscription
            {
                TopicAsTopicName = 
                    TopicName.FromProjectTopic(subscription.TopicAttributes!.ProjectId ?? projectId,
                        subscription.TopicAttributes.Name),
                SubscriptionName = subscriptionName,
                AckDeadlineSeconds = subscription.AckDeadlineSeconds,
                RetainAckedMessages = subscription.RetainAckedMessages,
                EnableMessageOrdering = subscription.EnableMessageOrdering,
                EnableExactlyOnceDelivery = subscription.EnableExactlyOnceDelivery,
                State = Google.Cloud.PubSub.V1.Subscription.Types.State.Active
            };

            if (subscription.MessageRetentionDuration != null)
            {
                gpcSubscription.MessageRetentionDuration =
                    Duration.FromTimeSpan(subscription.MessageRetentionDuration.Value);
            }

            if (subscription.Storage != null)
            {
                gpcSubscription.CloudStorageConfig = subscription.Storage;
            }

            if (subscription.ExpirationPolicy != null)
            {
                gpcSubscription.ExpirationPolicy = subscription.ExpirationPolicy;
            }

            if (!string.IsNullOrEmpty(subscription.DeadLetterTopic))
            {
                if (!TopicName.TryParse(subscription.DeadLetterTopic, false, out var deadLetterTopic))
                {
                    deadLetterTopic = TopicName.FromProjectTopic(subscription.ProjectId ?? projectId,
                        subscription.DeadLetterTopic);
                }

                gpcSubscription.DeadLetterPolicy = new DeadLetterPolicy
                {
                    MaxDeliveryAttempts = subscription.RequeueCount, DeadLetterTopic = deadLetterTopic.ToString()
                };
            }

            if (subscription.RequeueDelay != TimeSpan.Zero)
            {
                gpcSubscription.RetryPolicy = new RetryPolicy
                {
                    MinimumBackoff = Duration.FromTimeSpan(subscription.RequeueDelay),
                    MaximumBackoff = Duration.FromTimeSpan(subscription.MaxRequeueDelay)
                };
            }

            gpcSubscription.Labels.Add("Source", "Brighter");
            foreach (var label in subscription.TopicAttributes.Labels)
            {
                gpcSubscription.Labels[label.Key] = label.Value;
            }

            return gpcSubscription;
        }

        static async Task<bool> DoesSubscriptionExists(SubscriberServiceApiClient client,
            Google.Cloud.PubSub.V1.SubscriptionName name)
        {
            try
            {
                var sub = await client.GetSubscriptionAsync(name);
                return sub != null;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return false;
            }
        }
    }
    
    protected TopicName GetTopicName(string? projectId, string topicName) 
        => TopicName.FromProjectTopic(projectId ?? Connection.ProjectId, topicName);

    protected Google.Cloud.PubSub.V1.SubscriptionName GetSubscriptionName(string? projectId, string subscriptionName)
        => Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(projectId ?? Connection.ProjectId, subscriptionName);
}
