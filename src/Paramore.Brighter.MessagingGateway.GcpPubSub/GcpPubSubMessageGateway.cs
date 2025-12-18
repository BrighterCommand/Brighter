using System.Collections.Concurrent;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.Iam.V1;
using Google.Cloud.PubSub.V1;
using Google.Cloud.ResourceManager.V3;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// An abstract base class providing common functionality for interacting with the Google Cloud Pub/Sub service,
/// specifically for managing topic and subscription infrastructure. It handles checking for existence,
/// creating, and updating these resources based on Brighter's configuration.
/// </summary>
public abstract class GcpPubSubMessageGateway(GcpMessagingGatewayConnection connection)
{
    // A thread-safe, static dictionary to track which topics or subscriptions have already been
    // checked/created/updated. This prevents redundant API calls during factory creation.
    private static readonly ConcurrentDictionary<string, bool> s_topicOrSubscriptionAlreadyCreatedUpdate = new();

    /// <summary>
    /// Gets the configured connection details for the Google Cloud Pub/Sub gateway.
    /// </summary>
    protected GcpMessagingGatewayConnection Connection { get; } = connection;

    /// <summary>
    /// Asynchronously ensures that a Google Cloud Pub/Sub Topic exists and is configured according to the publication attributes.
    /// </summary>
    /// <param name="publication">The attributes of the topic to create or update.</param>
    /// <param name="makeChannel">Defines the behavior if the channel (topic) is missing: Assume, Validate, or Create.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="OnMissingChannel.Validate"/> is set and the topic is not found.</exception>
    public async Task EnsureTopicExistAsync(TopicAttributes publication, OnMissingChannel makeChannel)
    {
        if (makeChannel == OnMissingChannel.Assume)
        {
            // If we assume the infrastructure exists, skip the check and creation/update logic
            return;
        }

        var topicName = GetTopicName(publication.ProjectId, publication.Name);

        // Use the dictionary to ensure we only check/update this topic once per application run
        if (!s_topicOrSubscriptionAlreadyCreatedUpdate.TryAdd(topicName.ToString(), true))
        {
            return;
        }

        var publisher = await Connection.CreatePublisherServiceApiClientAsync();
        var topic = await GetGcpTopicExistAsync(publisher, topicName);

        if (makeChannel == OnMissingChannel.Validate)
        {
            if (topic != null)
            {
                return;
            }

            throw new InvalidOperationException($"Topic validation error: could not find topic {topicName}. Did you want Brighter to create infrastructure?");
        }

        // If we are here, makeChannel is OnMissingChannel.Create
        if (topic != null)
        {
            await UpdateTopicAsync(publisher, topic, publication);
        }
        else
        {
            await CreateTopicAsync(publisher, topicName, publication);
        }

        return;

        // Local function to create the topic on GCP
        static async Task CreateTopicAsync(PublisherServiceApiClient publisher, TopicName topicName, TopicAttributes attributes)
        {
            await publisher.CreateTopicAsync(CreateTopic(topicName, attributes));
        }

        // Local function to update an existing topic on GCP
        static async Task UpdateTopicAsync(PublisherServiceApiClient publisher, Topic topic, TopicAttributes attributes)
        {
            var mask = new FieldMask();

            // Only include paths for fields we might update
            mask.Paths.Add("labels");
            mask.Paths.Add("schema_settings");
            mask.Paths.Add("message_retention_duration");
            mask.Paths.Add("kms_key_name");
            mask.Paths.Add("message_storage_policy");

            if (attributes.StorePolicy != null)
            {
                topic.MessageStoragePolicy = attributes.StorePolicy;
            }

            if (attributes.MessageRetentionDuration != null)
            {
                topic.MessageRetentionDuration = Duration.FromTimeSpan(attributes.MessageRetentionDuration.Value);
            }

            if (!string.IsNullOrEmpty(attributes.KmsKeyName))
            {
                topic.KmsKeyName = attributes.KmsKeyName;
            }

            if (attributes.SchemaSettings != null)
            {
                topic.SchemaSettings = attributes.SchemaSettings;
            }

            // Ensure Brighter identifier label is present
            topic.Labels["source"] = "brighter";

            // Add or update custom labels
            foreach (var label in attributes.Labels)
            {
                topic.Labels[label.Key] = label.Value;
            }
            
            attributes.TopicConfiguration?.Invoke(topic);
            attributes.UpdateMaskConfiguration?.Invoke(mask);

            var updateTopic = new UpdateTopicRequest { Topic = topic, UpdateMask = mask };
            await publisher.UpdateTopicAsync(updateTopic);
        }

        // Local function to build a new Google Pub/Sub Topic object from Brighter attributes
        static Topic CreateTopic(TopicName topicName, TopicAttributes attributes)
        {
            var topic = new Topic { TopicName = topicName, State = Topic.Types.State.Active };

            if (attributes.StorePolicy != null)
            {
                topic.MessageStoragePolicy = attributes.StorePolicy;
            }

            if (attributes.MessageRetentionDuration != null)
            {
                topic.MessageRetentionDuration = Duration.FromTimeSpan(attributes.MessageRetentionDuration.Value);
            }

            if (!string.IsNullOrEmpty(attributes.KmsKeyName))
            {
                topic.KmsKeyName = attributes.KmsKeyName;
            }

            if (attributes.SchemaSettings != null)
            {
                topic.SchemaSettings = attributes.SchemaSettings;
            }

            // Ensure Brighter identifier label is present
            topic.Labels.Add("source", "brighter");
            foreach (var label in attributes.Labels)
            {
                topic.Labels[label.Key] = label.Value;
            }
            
            attributes.TopicConfiguration?.Invoke(topic);
            return topic;
        }
    }

    // Helper function to check for the existence of a topic, swallowing 'NotFound' RpcExceptions
    private static async Task<Topic?> GetGcpTopicExistAsync(PublisherServiceApiClient client, TopicName topicName)
    {
        try
        {
            return await client.GetTopicAsync(topicName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Asynchronously ensures that a Google Cloud Pub/Sub Subscription exists and is configured according to the subscription details.
    /// This method also handles the creation/configuration of Dead Letter Queue (DLQ) components and required IAM roles.
    /// </summary>
    /// <param name="pubSubSubscription">The Brighter-specific Pub/Sub subscription details.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="OnMissingChannel.Validate"/> is set and the subscription is not found.</exception>
    internal async Task EnsureSubscriptionExistsAsync(GcpPubSubSubscription pubSubSubscription)
    {
        if (pubSubSubscription.MakeChannels == OnMissingChannel.Assume)
        {
            // If we assume the infrastructure exists, skip the check and creation/update logic
            return;
        }

        var projectId = pubSubSubscription.ProjectId ?? Connection.ProjectId;

        // 1. Ensure the main Topic exists
        await EnsureTopicExistAsync(GetOrCreateTopicAttributes(pubSubSubscription, projectId), pubSubSubscription.MakeChannels);

        var subscriptionName = ParseOrCreateSubscriptionName();

        // Use the dictionary to ensure we only check/update this subscription once per application run
        if (!s_topicOrSubscriptionAlreadyCreatedUpdate.TryAdd(subscriptionName.ToString(), true))
        {
            return;
        }

        var client = await Connection.CreateSubscriberServiceApiClientAsync();
        var gcpSubscription = await GetSubscriptionAsync(client, subscriptionName);

        if (pubSubSubscription.MakeChannels == OnMissingChannel.Validate)
        {
            if (gcpSubscription != null)
            {
                return;
            }

            throw new InvalidOperationException($"No subscription found for {subscriptionName}");
        }

        // 2. Handle Dead Letter Queue (DLQ) setup if configured
        if (pubSubSubscription.DeadLetter != null)
        {
            // Recursively ensure the DLQ Topic and Subscription exist. DLQ creation should always be set to Create.
            await EnsureSubscriptionExistsAsync(new GcpPubSubSubscription(
                subscriptionName: new SubscriptionName($"dlq-{pubSubSubscription.Name.Value}"),
                channelName: pubSubSubscription.DeadLetter.Subscription,
                routingKey: pubSubSubscription.DeadLetter.TopicName,
                projectId: pubSubSubscription.ProjectId,
                ackDeadlineSeconds: pubSubSubscription.DeadLetter.AckDeadlineSeconds,
                makeChannels: OnMissingChannel.Create,
                messagePumpType: MessagePumpType.Proactor,
                requestType: pubSubSubscription.RequestType));

            // Set up the necessary IAM role for the main subscription to publish to the DLQ topic
            await UpdateIAmRoleForDeadLetterAsync(projectId, pubSubSubscription);
        }

        // 3. Create or Update the main Subscription
        if (gcpSubscription != null)
        {
            await UpdateSubscriptionAsync(projectId, gcpSubscription, pubSubSubscription);
        }
        else
        {
            await CreateSubscriptionAsync(projectId, subscriptionName, pubSubSubscription);
        }


        if (pubSubSubscription.DeadLetter != null)
        {
            await UpdateIAmRoleForSubscriptionAsync(projectId, subscriptionName, pubSubSubscription);
        }

        return;

        // Local function to parse or create the full SubscriptionName object
        Google.Cloud.PubSub.V1.SubscriptionName ParseOrCreateSubscriptionName()
        {
            if (Google.Cloud.PubSub.V1.SubscriptionName.TryParse(pubSubSubscription.ChannelName, out var gcpSubName))
            {
                return gcpSubName;
            }

            return Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(projectId, pubSubSubscription.ChannelName);
        }

        // Local function to get or create the TopicAttributes from the subscription
        static TopicAttributes GetOrCreateTopicAttributes(GcpPubSubSubscription pubSubSubscription, string projectId)
        {
            pubSubSubscription.TopicAttributes ??= new TopicAttributes();
            pubSubSubscription.TopicAttributes.ProjectId ??= projectId;
            if (string.IsNullOrEmpty(pubSubSubscription.TopicAttributes.Name))
            {
                pubSubSubscription.TopicAttributes.Name = pubSubSubscription.RoutingKey;
            }

            return pubSubSubscription.TopicAttributes;
        }

        // Local helper function to check for the existence of a subscription, swallowing 'NotFound' RpcExceptions
        static async Task<Google.Cloud.PubSub.V1.Subscription?> GetSubscriptionAsync(SubscriberServiceApiClient client,
            Google.Cloud.PubSub.V1.SubscriptionName name)
        {
            try
            {
                return await client.GetSubscriptionAsync(name);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Gets the canonical Google Cloud Pub/Sub <see cref="TopicName"/> object.
    /// </summary>
    /// <param name="projectId">The project ID for the topic. If null, uses the connection's default.</param>
    /// <param name="topicName">The name of the topic.</param>
    /// <returns>A <see cref="TopicName"/> instance.</returns>
    protected TopicName GetTopicName(string? projectId, string topicName)
        => TopicName.FromProjectTopic(projectId ?? Connection.ProjectId, topicName);

    /// <summary>
    /// Gets the canonical Google Cloud Pub/Sub <see cref="SubscriptionName"/> object.
    /// </summary>
    /// <param name="projectId">The project ID for the subscription. If null, uses the connection's default.</param>
    /// <param name="subscriptionName">The name of the subscription.</param>
    /// <returns>A <see cref="SubscriptionName"/> instance.</returns>
    protected Google.Cloud.PubSub.V1.SubscriptionName GetSubscriptionName(string? projectId, string subscriptionName)
        => Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(projectId ?? Connection.ProjectId,
            subscriptionName); 
        
    // Helper function to parse or create the TopicName object
    private static TopicName ParseOrCreateTopicName(string topicName, string projectId)
    {
        if (TopicName.TryParse(topicName, out var gcpTopicName))
        {
            return gcpTopicName;
        }

        return TopicName.FromProjectTopic(projectId, topicName);
    }

    // Asynchronously creates a new Google Cloud Pub/Sub subscription.
    private async Task CreateSubscriptionAsync(string projectId,
        Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
        GcpPubSubSubscription pubSubSubscription)
    {
        var topicName = pubSubSubscription.TopicAttributes!.Name;
        var topicProjectId = pubSubSubscription.TopicAttributes!.ProjectId ?? projectId;

        var gpcSubscription = new Google.Cloud.PubSub.V1.Subscription
        {
            TopicAsTopicName = ParseOrCreateTopicName(topicName, topicProjectId),
            SubscriptionName = subscriptionName,
            AckDeadlineSeconds = pubSubSubscription.AckDeadlineSeconds,
            RetainAckedMessages = pubSubSubscription.RetainAckedMessages,
            EnableMessageOrdering = pubSubSubscription.EnableMessageOrdering,
            EnableExactlyOnceDelivery = pubSubSubscription.EnableExactlyOnceDelivery,
            State = Google.Cloud.PubSub.V1.Subscription.Types.State.Active
        };

        if (pubSubSubscription.MessageRetentionDuration != null)
        {
            gpcSubscription.MessageRetentionDuration = Duration.FromTimeSpan(pubSubSubscription.MessageRetentionDuration.Value);
        }

        if (pubSubSubscription.Storage != null)
        {
            gpcSubscription.CloudStorageConfig = pubSubSubscription.Storage;
        }

        if (pubSubSubscription.ExpirationPolicy != null)
        {
            gpcSubscription.ExpirationPolicy = pubSubSubscription.ExpirationPolicy;
        }

        if (pubSubSubscription.DeadLetter != null)
        {
            // Configure Dead Letter Policy on the main subscription
            gpcSubscription.DeadLetterPolicy = new Google.Cloud.PubSub.V1.DeadLetterPolicy
            {
                MaxDeliveryAttempts = pubSubSubscription.DeadLetter.MaxDeliveryAttempts,
                DeadLetterTopic = ParseOrCreateTopicName(pubSubSubscription.DeadLetter.TopicName, projectId).ToString()
            };
        }

        if (pubSubSubscription.RequeueDelay != TimeSpan.Zero)
        {
            // Configure a retry policy (exponential backoff)
            gpcSubscription.RetryPolicy = new RetryPolicy
            {
                MinimumBackoff = Duration.FromTimeSpan(pubSubSubscription.RequeueDelay),
                MaximumBackoff = Duration.FromTimeSpan(pubSubSubscription.MaxRequeueDelay)
            };
        }

        gpcSubscription.Labels.Add("source", "brighter");
        foreach (var label in pubSubSubscription.Labels)
        {
            gpcSubscription.Labels[label.Key] = label.Value;
        }

        var client = await Connection.CreateSubscriberServiceApiClientAsync();
        await client.CreateSubscriptionAsync(gpcSubscription);
    }
    
    // Asynchronously updates an existing Google Cloud Pub/Sub subscription.
    private async Task UpdateSubscriptionAsync(string projectId,
        Google.Cloud.PubSub.V1.Subscription gcpSubscription,
        GcpPubSubSubscription pubSubSubscription)
    {
        var request = new UpdateSubscriptionRequest
        {
            Subscription = gcpSubscription, 
            UpdateMask = new FieldMask()
        };

        // Specify all fields that will be updated in the FieldMask
        request.UpdateMask.Paths.Add("ack_deadline_seconds");
        request.UpdateMask.Paths.Add("retain_acked_messages");
        request.UpdateMask.Paths.Add("enable_exactly_once_delivery");
        request.UpdateMask.Paths.Add("message_retention_duration");
        request.UpdateMask.Paths.Add("expiration_policy");
        request.UpdateMask.Paths.Add("dead_letter_policy");
        request.UpdateMask.Paths.Add("retry_policy");
        request.UpdateMask.Paths.Add("labels");
        request.UpdateMask.Paths.Add("cloud_storage_config");

        // Apply Brighter configuration to the Google subscription object
        gcpSubscription.AckDeadlineSeconds = pubSubSubscription.AckDeadlineSeconds;
        gcpSubscription.RetainAckedMessages = pubSubSubscription.RetainAckedMessages;
        gcpSubscription.EnableMessageOrdering = pubSubSubscription.EnableMessageOrdering;
        gcpSubscription.EnableExactlyOnceDelivery = pubSubSubscription.EnableExactlyOnceDelivery;
        gcpSubscription.State = Google.Cloud.PubSub.V1.Subscription.Types.State.Active;

        if (pubSubSubscription.MessageRetentionDuration != null)
        {
            gcpSubscription.MessageRetentionDuration =
                Duration.FromTimeSpan(pubSubSubscription.MessageRetentionDuration.Value);
        }

        if (pubSubSubscription.Storage != null)
        {
            gcpSubscription.CloudStorageConfig = pubSubSubscription.Storage;
        }

        if (pubSubSubscription.ExpirationPolicy != null)
        {
            gcpSubscription.ExpirationPolicy = pubSubSubscription.ExpirationPolicy;
        }

        if (pubSubSubscription.DeadLetter != null)
        {
            // Update Dead Letter Policy on the main subscription
            gcpSubscription.DeadLetterPolicy = new Google.Cloud.PubSub.V1.DeadLetterPolicy
            {
                MaxDeliveryAttempts = pubSubSubscription.DeadLetter.MaxDeliveryAttempts,
                DeadLetterTopic = ParseOrCreateTopicName(pubSubSubscription.DeadLetter.TopicName, projectId).ToString()
            };
        }
        else
        {
            // If DLQ is removed from config, set policy to null to disable
            gcpSubscription.DeadLetterPolicy = null;
        }

        if (pubSubSubscription.RequeueDelay != TimeSpan.Zero)
        {
            // Update retry policy
            gcpSubscription.RetryPolicy = new RetryPolicy
            {
                MinimumBackoff = Duration.FromTimeSpan(pubSubSubscription.RequeueDelay),
                MaximumBackoff = Duration.FromTimeSpan(pubSubSubscription.MaxRequeueDelay)
            };
        }
        else
        {
            // If retry is removed from config, set policy to null to disable
            gcpSubscription.RetryPolicy = null;
        }

        // Update labels
        gcpSubscription.Labels["source"] = "brighter";
        foreach (var label in pubSubSubscription.TopicAttributes?.Labels ?? [])
        {
            gcpSubscription.Labels[label.Key] = label.Value;
        }

        var client = await Connection.CreateSubscriberServiceApiClientAsync();
        await client.UpdateSubscriptionAsync(request);
    }

    // Asynchronously updates the IAM policy on the Dead Letter Topic (DLT) to grant the Pub/Sub service account
    // the `roles/pubsub.publisher` role, allowing the main subscription to forward messages to the DLT.
    private async Task UpdateIAmRoleForDeadLetterAsync(string projectId, GcpPubSubSubscription pubSubSubscription)
    {
        var publishMember = pubSubSubscription.DeadLetter!.PublisherMember;

        // If members aren't explicitly configured, derive the default Pub/Sub service account for the project
        if (string.IsNullOrEmpty(publishMember))
        {
            var projectClient = await Connection.CreateProjectsClientAsync();
            var project = await projectClient.GetProjectAsync(new GetProjectRequest { ProjectName = new ProjectName(projectId) });
                
            // The service account for Pub/Sub in a project is service-<PROJECT_NUMBER>@gcp-sa-pubsub.iam.gserviceaccount.com
            // The project number is the last segment of the Project resource name
            var projectNumber = project.Name.Split('/').Last();
            publishMember ??= $"serviceAccount:service-{projectNumber}@gcp-sa-pubsub.iam.gserviceaccount.com";
        }

        var topicName = ParseOrCreateTopicName(pubSubSubscription.DeadLetter.TopicName, projectId);
        var publisher = await Connection.CreatePublisherServiceApiClientAsync();

        // 1. Get the current IAM policy for the DLT
        var policy = await publisher.IAMPolicyClient.GetIamPolicyAsync(new GetIamPolicyRequest
        {
            ResourceAsResourceName = topicName,
        });

        var bindings = new List<Binding>();
        const string publisherRole = "roles/pubsub.publisher";
            
        // 2. Check if the DLT Publisher role is missing for the service account
        if (!policy.Bindings.Any(x => x.Role == publisherRole && x.Members.Contains(publishMember)))
        {
            // Add the new binding granting the Pub/Sub service account publisher permission
            var binding = new Binding { Role = publisherRole };
            binding.Members.Add(publishMember);
            bindings.Add(binding);
        }
        
        // 3. If any bindings were added, update the policy
        if (bindings.Count > 0)
        {
            policy.Bindings.AddRange(bindings);
            await publisher.IAMPolicyClient.SetIamPolicyAsync(new SetIamPolicyRequest
            {
                Policy = policy, ResourceAsResourceName = topicName
            });
        }
    }
    
    // Asynchronously updates the IAM policy on the Dead Letter Topic (DLT) to grant the Pub/Sub service account
    // the `roles/pubsub.publisher` role, allowing the main subscription to forward messages to the DLT.
    private async Task UpdateIAmRoleForSubscriptionAsync(string projectId, 
        Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
        GcpPubSubSubscription pubSubSubscription)
    {
        var subscriberMember = pubSubSubscription.SubscriberMember;

        // If members aren't explicitly configured, derive the default Pub/Sub service account for the project
        if (string.IsNullOrEmpty(subscriberMember))
        {
            var projectClient = await Connection.CreateProjectsClientAsync();
            var project = await projectClient.GetProjectAsync(new GetProjectRequest { ProjectName = new ProjectName(projectId) });

            // The service account for Pub/Sub in a project is service-<PROJECT_NUMBER>@gcp-sa-pubsub.iam.gserviceaccount.com
            // The project number is the last segment of the Project resource name
            var projectNumber = project.Name.Split('/').Last();
            subscriberMember ??= $"serviceAccount:service-{projectNumber}@gcp-sa-pubsub.iam.gserviceaccount.com";
        }

        var publisher = await Connection.CreatePublisherServiceApiClientAsync();

        // 1. Get the current IAM policy for the DLT
        var policy = await publisher.IAMPolicyClient.GetIamPolicyAsync(new GetIamPolicyRequest
        {
            ResourceAsResourceName = subscriptionName,
        });

        var bindings = new List<Binding>();
        
        const string subscriberRole = "roles/pubsub.subscriber";
        // Check if the DLT Subscriber role is missing for the service account
        if (!policy.Bindings.Any(x => x.Role == subscriberRole && x.Members.Contains(subscriberMember)))
        {
            // Note: The main Pub/Sub service account often needs subscriber role on the DLT for management/monitoring,
            // though the core DLQ function primarily relies on the publisher role.
            var binding = new Binding { Role = subscriberRole };
            binding.Members.Add(subscriberMember);
            bindings.Add(binding);
        }

        // 3. If any bindings were added, update the policy
        if (bindings.Count > 0)
        {
            policy.Bindings.AddRange(bindings);
            await publisher.IAMPolicyClient.SetIamPolicyAsync(new SetIamPolicyRequest
            {
                Policy = policy, ResourceAsResourceName = subscriptionName
            });
        }
    }
}
