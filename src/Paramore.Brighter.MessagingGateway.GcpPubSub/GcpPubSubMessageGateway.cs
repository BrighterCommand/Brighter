using System.Collections.Concurrent;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.Iam.V1;
using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Pub/Sub message gateway
/// </summary>
/// <param name="connection">The <see cref="GcpMessagingGatewayConnection"/>.</param>
public class GcpPubSubMessageGateway(GcpMessagingGatewayConnection connection)
{
    private static readonly ConcurrentDictionary<string, bool> s_topicOrSubscriptionAlreadyCreatedUpdate = new();

    /// <summary>
    /// The <see cref="GcpMessagingGatewayConnection"/>
    /// </summary>
    protected GcpMessagingGatewayConnection Connection { get; } = connection;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="publication"></param>
    /// <param name="makeChannel"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task EnsureTopicExistAsync(TopicAttributes publication, OnMissingChannel makeChannel)
    {
        if (makeChannel == OnMissingChannel.Assume)
        {
            return;
        }

        var topicName = GetTopicName(publication.ProjectId, publication.Name);
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

            throw new InvalidOperationException(
                $"Topic validation error: could not find topic {topicName}. Did you want Brighter to create infrastructure?");
        }

        if (topic != null)
        {
            await UpdateTopicAsync(publisher, topic, publication);
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

        static async Task UpdateTopicAsync(PublisherServiceApiClient publisher, Google.Cloud.PubSub.V1.Topic topic,
            TopicAttributes publication)
        {
            var mask = new FieldMask();

            mask.Paths.Add("labels");
            mask.Paths.Add("schema_settings");
            mask.Paths.Add("message_retention_duration");
            mask.Paths.Add("kms_key_name");

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

            topic.Labels["source"] = "brighter";

            foreach (var label in publication.Labels)
            {
                topic.Labels[label.Key] = label.Value;
            }

            var updateTopic = new UpdateTopicRequest { Topic = topic, UpdateMask = mask };
            await publisher.UpdateTopicAsync(updateTopic);
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

            topic.Labels.Add("source", "brighter");

            foreach (var label in publication.Labels)
            {
                topic.Labels[label.Key] = label.Value;
            }

            return topic;
        }
    }

    private static async Task<Google.Cloud.PubSub.V1.Topic?> GetGcpTopicExistAsync(PublisherServiceApiClient client,
        TopicName topicName)
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

    internal async Task EnsureSubscriptionExistsAsync(GcpSubscription subscription)
    {
        if (subscription.MakeChannels == OnMissingChannel.Assume)
        {
            return;
        }

        var projectId = subscription.ProjectId ?? Connection.ProjectId;
        await EnsureTopicExistAsync(GetOrCreateTopicAttributes(subscription, projectId), subscription.MakeChannels);

        var subscriptionName = ParseOrCreateSubscriptionName();
        if (!s_topicOrSubscriptionAlreadyCreatedUpdate.TryAdd(subscriptionName.ToString(), true))
        {
            return;
        }

        var client = await Connection.CreateSubscriberServiceApiClientAsync();
        var gcpSubscription = await GetSubscriptionAsync(client, subscriptionName);
        if (subscription.MakeChannels == OnMissingChannel.Validate)
        {
            if (gcpSubscription != null)
            {
                return;
            }

            throw new InvalidOperationException("No subscription found");
        }

        if (subscription.DeadLetter != null)
        {
            await EnsureSubscriptionExistsAsync(new GcpSubscription(
                subscriptionName: new SubscriptionName($"dlq-{subscription.Name.Value}"),
                channelName: subscription.DeadLetter.Subscription,
                routingKey: subscription.DeadLetter.TopicName,
                projectId: subscription.ProjectId,
                ackDeadlineSeconds: subscription.DeadLetter.AckDeadlineSeconds,
                makeChannels: OnMissingChannel.Create,
                messagePumpType: MessagePumpType.Proactor,
                requestType: subscription.RequestType));

            await UpdateIAmRoleForDeadLetterAsync(projectId, subscription);
        }

        if (gcpSubscription != null)
        {
            await UpdateSubscriptionAsync(projectId, gcpSubscription, subscription);
        }
        else
        {
            await CreateSubscriptionAsync(projectId, subscriptionName, subscription);
        }

        return;

        Google.Cloud.PubSub.V1.SubscriptionName ParseOrCreateSubscriptionName()
        {
            if (Google.Cloud.PubSub.V1.SubscriptionName.TryParse(subscription.ChannelName, out var gcpSubName))
            {
                return gcpSubName;
            }

            return Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(projectId, subscription.ChannelName);
        }

        static TopicAttributes GetOrCreateTopicAttributes(GcpSubscription subscription, string projectId)
        {
            subscription.TopicAttributes ??= new TopicAttributes();
            subscription.TopicAttributes.ProjectId ??= projectId;
            if (string.IsNullOrEmpty(subscription.TopicAttributes.Name))
            {
                subscription.TopicAttributes.Name = subscription.RoutingKey;
            }

            return subscription.TopicAttributes;
        }

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

    protected TopicName GetTopicName(string? projectId, string topicName)
        => TopicName.FromProjectTopic(projectId ?? Connection.ProjectId, topicName);

    protected Google.Cloud.PubSub.V1.SubscriptionName GetSubscriptionName(string? projectId, string subscriptionName)
        => Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(projectId ?? Connection.ProjectId,
            subscriptionName); 
    
    private static TopicName ParseOrCreateTopicName(string topicName, string projectId)
    {
        if (TopicName.TryParse(topicName, out var gcpTopicName))
        {
            return gcpTopicName;
        }

        return TopicName.FromProjectTopic(projectId, topicName);
    }

    private async Task CreateSubscriptionAsync(string projectId,
        Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
        GcpSubscription subscription)
    {
        var topicName = subscription.TopicAttributes!.Name;
        var topicProjectId = subscription.TopicAttributes!.ProjectId ?? projectId;
        var gpcSubscription = new Google.Cloud.PubSub.V1.Subscription
        {
            TopicAsTopicName = ParseOrCreateTopicName(topicName, topicProjectId),
            SubscriptionName = subscriptionName,
            AckDeadlineSeconds = subscription.AckDeadlineSeconds,
            RetainAckedMessages = subscription.RetainAckedMessages,
            EnableMessageOrdering = subscription.EnableMessageOrdering,
            EnableExactlyOnceDelivery = subscription.EnableExactlyOnceDelivery,
            State = Google.Cloud.PubSub.V1.Subscription.Types.State.Active
        };

        if (subscription.MessageRetentionDuration != null)
        {
            gpcSubscription.MessageRetentionDuration = Duration.FromTimeSpan(subscription.MessageRetentionDuration.Value);
        }

        if (subscription.Storage != null)
        {
            gpcSubscription.CloudStorageConfig = subscription.Storage;
        }

        if (subscription.ExpirationPolicy != null)
        {
            gpcSubscription.ExpirationPolicy = subscription.ExpirationPolicy;
        }

        if (subscription.DeadLetter != null)
        {
            gpcSubscription.DeadLetterPolicy = new Google.Cloud.PubSub.V1.DeadLetterPolicy
            {
                MaxDeliveryAttempts = subscription.DeadLetter.MaxDeliveryAttempts,
                DeadLetterTopic = ParseOrCreateTopicName(subscription.DeadLetter.TopicName, projectId).ToString()
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

        gpcSubscription.Labels.Add("source", "brighter");
        foreach (var label in subscription.Labels)
        {
            gpcSubscription.Labels[label.Key] = label.Value;
        }

        var client = await Connection.CreateSubscriberServiceApiClientAsync();
        await client.CreateSubscriptionAsync(gpcSubscription);
    }
    
     private async Task UpdateSubscriptionAsync(string projectId,
            Google.Cloud.PubSub.V1.Subscription gcpSubscription,
            GcpSubscription subscription)
     {
         var request = new UpdateSubscriptionRequest
         {
             Subscription = gcpSubscription, UpdateMask = new FieldMask()
         };

         request.UpdateMask.Paths.Add("ack_deadline_seconds");
         request.UpdateMask.Paths.Add("retain_acked_messages");
         request.UpdateMask.Paths.Add("enable_exactly_once_delivery");
         request.UpdateMask.Paths.Add("message_retention_duration");
         request.UpdateMask.Paths.Add("expiration_policy");
         request.UpdateMask.Paths.Add("dead_letter_policy");
         request.UpdateMask.Paths.Add("retry_policy");
         request.UpdateMask.Paths.Add("labels");

         gcpSubscription.AckDeadlineSeconds = subscription.AckDeadlineSeconds;
         gcpSubscription.RetainAckedMessages = subscription.RetainAckedMessages;
         gcpSubscription.EnableMessageOrdering = subscription.EnableMessageOrdering;
         gcpSubscription.EnableExactlyOnceDelivery = subscription.EnableExactlyOnceDelivery;
         gcpSubscription.State = Google.Cloud.PubSub.V1.Subscription.Types.State.Active;

         if (subscription.MessageRetentionDuration != null)
         {
             gcpSubscription.MessageRetentionDuration =
                 Duration.FromTimeSpan(subscription.MessageRetentionDuration.Value);
         }

         if (subscription.Storage != null)
         {
             gcpSubscription.CloudStorageConfig = subscription.Storage;
         }

         if (subscription.ExpirationPolicy != null)
         {
             gcpSubscription.ExpirationPolicy = subscription.ExpirationPolicy;
         }

         if (subscription.DeadLetter != null)
         {
             gcpSubscription.DeadLetterPolicy = new Google.Cloud.PubSub.V1.DeadLetterPolicy
             {
                 MaxDeliveryAttempts = subscription.DeadLetter.MaxDeliveryAttempts,
                 DeadLetterTopic = ParseOrCreateTopicName(subscription.DeadLetter.TopicName, projectId).ToString()
             };
         }

         if (subscription.RequeueDelay != TimeSpan.Zero)
         {
             gcpSubscription.RetryPolicy = new RetryPolicy
             {
                 MinimumBackoff = Duration.FromTimeSpan(subscription.RequeueDelay),
                 MaximumBackoff = Duration.FromTimeSpan(subscription.MaxRequeueDelay)
             };
         }

         gcpSubscription.Labels["source"] = "brighter";
         foreach (var label in subscription.TopicAttributes?.Labels ?? [])
         {
             gcpSubscription.Labels[label.Key] = label.Value;
         }

         var client = await Connection.CreateSubscriberServiceApiClientAsync();
         await client.UpdateSubscriptionAsync(request);
     }

     private async Task UpdateIAmRoleForDeadLetterAsync(string projectId, GcpSubscription subscription)
     {
         var publishMember = subscription.DeadLetter!.PublisherMember;
         var subscriberMember = subscription.DeadLetter.SubscriberMember;
         if (string.IsNullOrEmpty(publishMember) || string.IsNullOrEmpty(subscriberMember))
         {
             var projectClient = await Connection.CreateProjectsClientAsync();
             var project = await projectClient.GetProjectAsync(new ProjectName(projectId));
             var projectNumber = project.Name.Split('/').Last();
             publishMember ??= $"serviceAccount:service-{projectNumber}@gcp-sa-pubsub.iam.gserviceaccount.com";
             subscriberMember ??= $"serviceAccount:service-{projectNumber}@gcp-sa-pubsub.iam.gserviceaccount.com";
         }

         var topicName = ParseOrCreateTopicName(subscription.DeadLetter.TopicName, projectId);
         var publisher = await Connection.CreatePublisherServiceApiClientAsync();
         var policy = await publisher.IAMPolicyClient.GetIamPolicyAsync(new GetIamPolicyRequest
         {
             ResourceAsResourceName = topicName,
         });

         var bindings = new List<Binding>();
         const string publisherRole = "roles/pubsub.publisher";
         if (!policy.Bindings.Any(x => x.Role == publisherRole && x.Members.Contains(publishMember)))
         {
             var binding = new Binding { Role = publisherRole };
             binding.Members.Add(publishMember);
             bindings.Add(binding);
         }

         const string subscriberRole = "roles/pubsub.subscriber";
         if (!policy.Bindings.Any(x => x.Role == subscriberRole && x.Members.Contains(subscriberMember)))
         {
             var binding = new Binding { Role = publisherRole };
             binding.Members.Add(subscriberMember);
             bindings.Add(binding);
         }

         if (bindings.Count > 0)
         {
             policy.Bindings.AddRange(bindings);
             await publisher.IAMPolicyClient.SetIamPolicyAsync(new SetIamPolicyRequest
             {
                 Policy = policy, ResourceAsResourceName = topicName
             });
         }
     }
}
