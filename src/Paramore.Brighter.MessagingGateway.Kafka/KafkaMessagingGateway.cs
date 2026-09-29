#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    /// <summary>
    /// Base class for communicating with a Kafka broker. Derived types are <see cref="KafkaMessageProducer"/> and <see cref="KafkaMessageConsumer"/>
    /// This base class mainly handles how we confirm required infrastructure - topics - and depending on the MakeChannels field, will either
    /// create missing infrastructure, validate infrastructure exists, or just assume that infrastructure exists.
    /// </summary>
    public partial class KafkaMessagingGateway
    {
        protected static readonly ILogger s_logger = ApplicationLogging.CreateLogger<KafkaMessageProducer>();
        protected ClientConfig? ClientConfig;
        protected OnMissingChannel MakeChannels;
        protected RoutingKey? Topic;
        protected int NumPartitions;
        protected short ReplicationFactor;
        protected TimeSpan TopicFindTimeout;

        /// <summary>
        /// Ensure that the topic exists,  behaviour based on the MakeChannels flag of the publication
        /// Sync over async, but alright as we in topic creation
        /// </summary>
        /// <exception cref="ChannelFailureException"></exception>
        protected void EnsureTopic()
        {
            if (MakeChannels == OnMissingChannel.Assume)
                return;

            if (MakeChannels == OnMissingChannel.Validate || MakeChannels == OnMissingChannel.Create)
            {
                var exists = FindTopic();

                if (!exists && MakeChannels == OnMissingChannel.Validate)
                {
                    var topic = Topic is not null ? new RoutingKey(Topic.Value) : RoutingKey.Empty;
                    throw new ChannelFailureException($"Topic: {topic} does not exist");
                }

                if (!exists && MakeChannels == OnMissingChannel.Create)
                    BrighterAsyncContext.Run(() => MakeTopic());
            }
        }

        private async Task MakeTopic()
        {
            if (RoutingKey.IsNullOrEmpty(Topic)) throw new InvalidOperationException("Topic cannot be null");

            using var adminClient = new AdminClientBuilder(ClientConfig).Build();
            try
            {
                await adminClient.CreateTopicsAsync(new List<TopicSpecification>
                {
                    new()
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

                Log.TopicAlreadyExists(s_logger, Topic.Value);
            }

            // A topic just created (or found to already exist, moments earlier, by another caller)
            // may not yet be visible in this broker's own metadata: CreateTopics returns once the
            // controller accepts the request, not once the broker's local metadata image reflects
            // it. Wait, best-effort and bounded by TopicFindTimeout, for that visibility before
            // returning, so a consumer that subscribes immediately after does not race the topic's
            // own creation.
            await WaitForTopicVisible(adminClient);
        }

        private async Task WaitForTopicVisible(IAdminClient adminClient)
        {
            if (RoutingKey.IsNullOrEmpty(Topic)) throw new InvalidOperationException("Topic cannot be null");

            var stopwatch = Stopwatch.StartNew();
            var pollTimeout = TimeSpan.FromMilliseconds(Math.Min(500, TopicFindTimeout.TotalMilliseconds));

            while (stopwatch.Elapsed < TopicFindTimeout)
            {
                var metadata = adminClient.GetMetadata(Topic.Value, pollTimeout);
                var matchingTopic = metadata.Topics.FirstOrDefault(tp => tp.Topic == Topic.Value);

                var visible = matchingTopic is { Error.Code: ErrorCode.NoError }
                    && matchingTopic.Partitions.Count == NumPartitions
                    && matchingTopic.Partitions.All(partition => partition.Leader >= 0);

                if (visible)
                    return;

                await Task.Delay(50);
            }
        }

        private bool FindTopic()
        {
            if (RoutingKey.IsNullOrEmpty(Topic)) throw new InvalidOperationException("Topic cannot be null");
            
            using var adminClient = new AdminClientBuilder(ClientConfig).Build();
            try
            {
                bool found = false;

                var metadata = adminClient.GetMetadata(Topic.Value, TopicFindTimeout);
                //confirm we are in the list
                var matchingTopics = metadata.Topics.Where(tp => tp.Topic == Topic.Value).ToArray();
                if (matchingTopics.Length > 0)
                {
                    var matchingTopic = matchingTopics[0];
                        
                    //was it found?
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
                                error += $" topic is in error => {matchingTopic.Error!.Reason};";
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

                            Log.TopicMisconfiguredWarning(s_logger, error);
                        }
                    }
                }

                if (found)
                    Log.TopicExists(s_logger, Topic.Value);
                    
                return found;
            }
            catch (Exception e)
            {
                throw new ChannelFailureException($"Error finding topic {Topic.Value}", e);
            }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "Topic {Topic} already exists")]
            public static partial void TopicAlreadyExists(ILogger logger, string topic);

            [LoggerMessage(LogLevel.Warning, "{TopicMisconfiguredError}")]
            public static partial void TopicMisconfiguredWarning(ILogger logger, string topicMisconfiguredError);
            
            [LoggerMessage(LogLevel.Information, "Topic {Topic} exists")]
            public static partial void TopicExists(ILogger logger, string topic);
        }
    }
}

