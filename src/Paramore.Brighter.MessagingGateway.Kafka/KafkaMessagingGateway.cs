using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public class KafkaMessagingGateway
    {
        protected static readonly ILogger s_logger = ApplicationLogging.CreateLogger<KafkaMessageProducer>();
        protected ClientConfig _clientConfig;
        protected OnMissingChannel MakeChannels;
        protected RoutingKey Topic;
        protected int NumPartitions;
        protected short ReplicationFactor;
        protected int TopicFindTimeoutMs;

        protected void EnsureTopic()
        {
            if (MakeChannels == OnMissingChannel.Assume)
                return;

            if (MakeChannels == OnMissingChannel.Validate || MakeChannels == OnMissingChannel.Create)
            {
                var topicExists = FindTopic();
                if (!topicExists && MakeChannels == OnMissingChannel.Validate)
                    throw new ChannelFailureException($"Topic: {Topic.Value} does not exist");
                if (!topicExists && MakeChannels == OnMissingChannel.Create)
                    MakeTopic().GetAwaiter().GetResult();
            }
        }

        private async Task MakeTopic()
        {

            using (var adminClient = new AdminClientBuilder(_clientConfig).Build())
            {
                try
                {
                    await adminClient.CreateTopicsAsync(new List<TopicSpecification> {
                        new TopicSpecification
                        {
                            Name = Topic.Value, 
                            NumPartitions = NumPartitions, 
                            ReplicationFactor = ReplicationFactor
                        } });
                }
                catch (CreateTopicsException e)
                {
                    if (e.Results[0].Error.Code != ErrorCode.TopicAlreadyExists)
                    {
                        throw new ChannelFailureException($"An error occured creating topic {Topic.Value}: {e.Results[0].Error.Reason}");
                    }

                    s_logger.LogDebug("Topic {Topic} already exists", Topic.Value);
                }
            }
        }

        private bool FindTopic()
        {
            using (var adminClient = new AdminClientBuilder(_clientConfig).Build())
            {
                try
                {
                    var metadata = adminClient.GetMetadata(Topic.Value, TimeSpan.FromMilliseconds(TopicFindTimeoutMs));
                    //confirm we are in the list
                    var matchingTopics = metadata.Topics.Where(tp => tp.Topic ==Topic.Value).ToArray();
                    if (matchingTopics.Length > 0)
                    {
                        var matchingTopic = matchingTopics[0];
                        if (matchingTopic.Error == null) return true;
                        if (matchingTopic.Error.Code == ErrorCode.NoError) return true;
                        if (matchingTopic.Error.Code == ErrorCode.UnknownTopicOrPart)
                            return false;
                        else
                        {
                            s_logger.LogWarning("Topic {Topic} is in error with code: {ErrorCode} and reason: {ErrorMessage}", matchingTopic.Topic, matchingTopic.Error.Code, matchingTopic.Error.Reason);
                            return false;
                        }

                    }

                    return false;
                }
                catch (Exception e)
                {
                    throw new ChannelFailureException($"Error finding topic {Topic.Value}", e);
                }
            } 
        }
    }
}
