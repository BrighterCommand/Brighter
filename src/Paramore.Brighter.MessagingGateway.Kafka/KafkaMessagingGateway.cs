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
    /// <summary>
    /// Base class for communicating with a Kafka broker. Derived types are <see cref="KafkaMessageProducer"/> and <see cref="KafkaMessageConsumer"/>
    /// This base class mainly handles how we confirm required infrastructure - topics - and depending on the MakeChannels field, will either
    /// create missing infrastructure, validate infrastructure exists, or just assume that infrastructure exists.
    /// </summary>
    public class KafkaMessagingGateway
    {
        protected static readonly ILogger s_logger = ApplicationLogging.CreateLogger<KafkaMessageProducer>();
        protected ClientConfig ClientConfig;
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
                var exists = FindTopic();

                if (!exists && MakeChannels == OnMissingChannel.Validate)
                    throw new ChannelFailureException($"Topic: {Topic.Value} does not exist");

                if (!exists && MakeChannels == OnMissingChannel.Create)
                    MakeTopic().GetAwaiter().GetResult();
            }
        }

        private async Task MakeTopic()
        {
            using (var adminClient = new AdminClientBuilder(ClientConfig).Build())
            {
                try
                {
                    await adminClient.CreateTopicsAsync(new List<TopicSpecification>
                    {
                        new TopicSpecification
                        {
                            Name = Topic.Value,
                            NumPartitions = NumPartitions,
                            ReplicationFactor = ReplicationFactor
                        }
                    });
                }
                catch (CreateTopicsException e)
                {
                    if (e.Results[0].Error.Code != ErrorCode.TopicAlreadyExists)
                    {
                        throw new ChannelFailureException(
                            $"An error occured creating topic {Topic.Value}: {e.Results[0].Error.Reason}");
                    }

                    s_logger.LogDebug("Topic {Topic} already exists", Topic.Value);
                }
            }
        }

        private bool FindTopic()
        {
            using (var adminClient = new AdminClientBuilder(ClientConfig).Build())
            {
                try
                {
                    bool found = false;

                    var metadata = adminClient.GetMetadata(Topic.Value, TimeSpan.FromMilliseconds(TopicFindTimeoutMs));
                    //confirm we are in the list
                    var matchingTopics = metadata.Topics.Where(tp => tp.Topic == Topic.Value).ToArray();
                    if (matchingTopics.Length > 0)
                    {
                        found = true;
                        var matchingTopic = matchingTopics[0];
                        
                        //was it really found?
                        found = matchingTopic.Error != null && matchingTopic.Error.Code != ErrorCode.UnknownTopicOrPart;
                        if (found)
                        {
                            //is it in error, and does it have required number of partitions or replicas
                            bool inError = matchingTopic.Error != null && matchingTopic.Error.Code != ErrorCode.NoError;
                            bool matchingPartitions = matchingTopic.Partitions.Count == NumPartitions;
                            bool replicated =
                                matchingTopic.Partitions.All(
                                    partition => partition.Replicas.Length == ReplicationFactor);

                            bool valid = !inError && matchingPartitions && replicated;

                            if (!valid)
                            {
                                string error = "Topic exists but does not match publication: ";
                                //if topic is in error
                                if (inError)
                                {
                                    error += $" topic is in error => {matchingTopic.Error.Reason};";
                                }

                                if (!matchingPartitions)
                                {
                                    error +=
                                        $"topic is misconfigured => NumPartitions should be {NumPartitions} but is {matchingTopic.Partitions.Count};";
                                }

                                if (!replicated)
                                {
                                    error +=
                                        $"topic is misconfigured => ReplicationFactor should be {ReplicationFactor} but is {matchingTopic.Partitions[0].Replicas.Length};";
                                }

                                s_logger.LogWarning(error);
                            }
                        }
                    }

                    if (found)
                        s_logger.LogInformation($"Topic {Topic.Value} exists");
                    
                    return found;
                }
                catch (Exception e)
                {
                    throw new ChannelFailureException($"Error finding topic {Topic.Value}", e);
                }
            }
        }
    }
}
