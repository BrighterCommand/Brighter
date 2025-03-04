using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

public class PubSubMessageGateway
{
    public PubSubMessageGateway(GcpMessagingGatewayConnection connection)
    {
        Connection = connection;
    }

    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PubSubMessageGateway>();
    protected GcpMessagingGatewayConnection Connection { get; }


    internal async Task EnsureTopicExistAsync(TopicAttributes publication)
    {
        var topicName = TopicName.FromProjectTopic(publication.ProjectId ?? Connection.ProjectId, publication.Name);
        if (publication.MakeChannels == OnMissingChannel.Assume)
        {
            return;
        }

        var publisher = await PublisherServiceApiClient.CreateAsync();
        if (await DoesTopicExistAsync(publisher, topicName.ProjectId, topicName.TopicId))
        {
            return;
        }

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            // TODO: Change exception
            throw new(
                $"Topic validation error: could not find topic {topicName}. Did you want Brighter to create infrastructure?");
        }

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

        await publisher.CreateTopicAsync(topic);
    }

    private static async Task<bool> DoesTopicExistAsync(PublisherServiceApiClient client, string projectId,
        string topicId)
    {
        var topics = client.ListTopicsAsync(new ListTopicsRequest { Project = projectId });
        foreach (var topic in await topics.ReadPageAsync(100))
        {
            if (topic.TopicName.TopicId == topicId)
            {
                return true;
            }
        }

        return false;
    }


    internal async Task EnsureSubscriptionExistsAsync(PubSubSubscription subscription)
    {
        if (subscription.MakeChannels == OnMissingChannel.Assume)
        {
            return;
        }

        subscription.TopicAttributes ??= new TopicAttributes { MakeChannels = subscription.MakeChannels };

        subscription.TopicAttributes.ProjectId ??= Connection.ProjectId;
        if (string.IsNullOrEmpty(subscription.TopicAttributes.Name))
        {
            subscription.TopicAttributes.Name = subscription.ChannelName.Value;
        }

        await EnsureTopicExistAsync(subscription.TopicAttributes!);

        var subscriptionName = Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(
            subscription.ProjectId ?? Connection.ProjectId,
            subscription.ChannelName.Value);

        var client = await SubscriberServiceApiClient.CreateAsync();
        var gpcSubscription = await client.GetSubscriptionAsync(subscriptionName);
        if (gpcSubscription != null)
        {
            return;
        }

        if (subscription.MakeChannels == OnMissingChannel.Validate)
        {
            throw new Exception("No subscription found");
        }

        gpcSubscription = new Google.Cloud.PubSub.V1.Subscription
        {
            TopicAsTopicName =
                TopicName.FromProjectTopic(subscription.TopicAttributes.ProjectId,
                    subscription.TopicAttributes.Name!),
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
                deadLetterTopic = TopicName.FromProjectTopic(subscription.ProjectId ?? Connection.ProjectId,
                    subscription.DeadLetterTopic);
            }

            gpcSubscription.DeadLetterPolicy = new DeadLetterPolicy
            {
                MaxDeliveryAttempts = subscription.RequeueCount, 
                DeadLetterTopic = deadLetterTopic.ToString()
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

        await client.CreateSubscriptionAsync(gpcSubscription);
    }
}
