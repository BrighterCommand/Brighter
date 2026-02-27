#region Licence
/* The MIT License (MIT)
Copyright © 2015 Wayne Hunsley <whunsley@gmail.com>

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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    /// <inheritdoc cref="IAmAMessageConsumerSync" />
    /// <summary>
    /// Class KafkaMessageConsumer is an implementation of <see cref="IAmAMessageConsumerSync"/>
    /// and provides the facilities to consume messages from a Kafka broker for a topic
    /// in a consumer group.
    /// A Kafka Message Consumer can create topics, depending on the options chosen.
    /// We store an offset when a message is acknowledged, the message pump does this after successfully invoking a handler.
    /// We commit offsets when the batch size is reached, or the sweeper decides it is too long between commits.
    /// This dual strategy prevents low traffic topics having batches that are 'pending' for long periods, causing a risk that the consumer
    /// will end before committing its offsets.
    /// </summary>
    public partial class KafkaMessageConsumer : KafkaMessagingGateway, IAmAMessageConsumerSync, IAmAMessageConsumerAsync
    {
        private readonly KafkaMessagingGatewayConfiguration _configuration;
        private readonly IConsumer<string, byte[]> _consumer;
        private readonly KafkaMessageCreator _creator;
        private readonly ConsumerConfig _consumerConfig;
        private List<TopicPartition> _partitions = [];
        private readonly ConcurrentBag<TopicPartitionOffset> _offsetStorage = [];
        private readonly long _maxBatchSize;
        private readonly TimeSpan _readCommittedOffsetsTimeout;
        private DateTime _lastFlushAt;
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _sweepUncommittedInterval;
        private readonly SemaphoreSlim _flushToken = new(1, 1);
        private readonly ITimer _sweeperTimer;
        private bool _hasFatalError;
        private bool _isClosed;
        private readonly RoutingKey? _deadLetterRoutingKey;
        private readonly RoutingKey? _invalidMessageRoutingKey;
        private readonly Lazy<KafkaMessageProducer?>? _deadLetterProducer;
        private readonly Lazy<KafkaMessageProducer?>? _invalidMessageProducer;
        private readonly IAmAMessageScheduler? _scheduler;
        private KafkaMessageProducer? _requeueProducer;
        private bool _requeueProducerInitialized;
        private object? _requeueProducerLock;


        /// <summary>
        /// Constructs a KafkaMessageConsumer using Confluent's Consumer Builder. We set up callbacks to handle assigned, revoked or lost partitions as
        /// well as errors. We handle storing and committing offsets, using a batch strategy to commit, with a sweeper thread to prevent partially complete
        /// batches lingering beyond a timeout threshold.
        /// </summary>
        /// <param name="configuration">The configuration tells us how to connect to the Broker. Required.</param>
        /// <param name="routingKey">The routing key is a the Kafka Topic to consume. Required.</param>
        /// <param name="groupId">The id of the consumer group we belong to. Required.</param>
        /// <param name="offsetDefault">When connecting to a stream, and we have not offset stored, where do we begin reading?
        /// Earliest - beginning of the stream; latest - anything after we connect. Defaults to Earliest</param>
        /// <param name="sessionTimeout">If we don't send a heartbeat within this interval, the broker terminates our session. Defaults to 10s</param>
        /// <param name="maxPollInterval">Maximum interval between consumer polls, Failing to poll at this interval marks the client as failed triggering a
        /// re-balance of the group. Defaults to 10s.  Note that we set the Confluent clients auto store offsets and auto commit to false and handle this
        /// within Brighter. We store an offset following an Ack, and we commit offsets at a batch or sweeper interval, whichever is first.</param>
        /// <param name="isolationLevel">Affects reading of transactionally written messages. Committed only reads those committed to all nodes,
        /// uncommitted reads messages that have been written to *some* node. Defaults to ReadCommitted</param>
        /// <param name="commitBatchSize">What size does a batch grow before we write commits that we have stored. Defaults to 10. If a consumer crashes,
        /// uncommitted offsets will not have been written and will be processed by the consumer group again. Conversely a low batch size increases writes
        /// and lowers performance.</param>
        /// <param name="sweepUncommittedOffsetsInterval">The <see cref="TimeSpan"/> interval for the sweeper to commit uncommitted offsets.
        /// The sweeper ensures that partially complete batches, particularly on low throughput queues will be written. It runs after the interval and 
        /// commits anything currently in the store and not committed. Defaults to 30s if not specified.</param>
        /// <param name="readCommittedOffsetsTimeout">Timeout when reading the committed offsets, used when closing a consumer to log where it reached.
        /// Defaults to 5000</param>
        /// <param name="numPartitions">If we are creating missing infrastructure, How many partitions should the topic have. Defaults to 1</param>
        /// <param name="partitionAssignmentStrategy">What is the strategy for assigning partitions to consumers?</param>
        /// <param name="replicationFactor">If we are creating missing infrastructure, how many in-sync replicas do we need. Defaults to 1</param>
        /// <param name="topicFindTimeout">If we are checking for the existence of the topic, what is the timeout. Defaults to 10000ms</param>
        /// <param name="makeChannels">Should we create infrastructure (topics) where it does not exist or check. Defaults to Create</param>
        /// <param name="configHook">Allows you to modify the Kafka client configuration before a consumer is created.</param>
        /// <param name="timeProvider">The <see cref="TimeProvider"/> used to create the timer for sweeping uncommitted offsets. Defaults to <see cref="TimeProvider.System"/> if not specified. Can be overridden for testing purposes.</param>
        /// <param name="deadLetterRoutingKey">If we support a dead letter topic what is the <see cref="RoutingKey"/></param>
        /// <param name="invalidMessageRoutingKey">If we support an invalid message topic what is the <see cref="RoutingKey"/></param>
        /// <param name="scheduler">Optional scheduler for delayed requeue operations. When provided, the lazily-created
        /// requeue producer will use this scheduler for delayed sends.</param>
        /// <exception cref="ConfigurationException">Throws an exception if required parameters missing</exception>
        public KafkaMessageConsumer(
            KafkaMessagingGatewayConfiguration configuration,
            RoutingKey routingKey,
            string? groupId,
            AutoOffsetReset offsetDefault = AutoOffsetReset.Earliest,
            TimeSpan? sessionTimeout = null,
            TimeSpan? maxPollInterval = null,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            long commitBatchSize = 10,
            TimeSpan? sweepUncommittedOffsetsInterval = null,
            TimeSpan? readCommittedOffsetsTimeout = null,
            int numPartitions = 1,
            PartitionAssignmentStrategy partitionAssignmentStrategy = PartitionAssignmentStrategy.RoundRobin,
            short replicationFactor = 1,
            TimeSpan? topicFindTimeout = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            Action<ConsumerConfig>? configHook = null,
            RoutingKey? deadLetterRoutingKey = null,
            RoutingKey? invalidMessageRoutingKey = null,
            TimeProvider? timeProvider = null,
            IAmAMessageScheduler? scheduler = null
            )
        {
            if (groupId is null)
                throw new ConfigurationException("You must set a GroupId for the consumer");
            
            Topic = routingKey ?? throw new ConfigurationException("You must set a RoutingKey as the Topic for the consumer");
            
            _configuration = configuration ?? throw new ConfigurationException("You must set a KafkaMessagingGatewayConfiguration to connect to a broker");
            _scheduler = scheduler;

            _deadLetterRoutingKey = deadLetterRoutingKey;
            _invalidMessageRoutingKey = invalidMessageRoutingKey;
            // LazyThreadSafetyMode.None: message pumps are single-threaded per consumer, so no
            // thread-safety mode is needed. None does not cache exceptions, allowing the factory
            // to retry on the next .Value access after a transient failure.
            if (_deadLetterRoutingKey != null)
            {
                _deadLetterProducer = new Lazy<KafkaMessageProducer?>(
                    () => CreateProducer(_deadLetterRoutingKey, Log.ErrorCreatingDLQ), LazyThreadSafetyMode.None);
            }

            if (_invalidMessageRoutingKey != null)
            {
                _invalidMessageProducer = new Lazy<KafkaMessageProducer?>(
                    () => CreateProducer(_invalidMessageRoutingKey, Log.ErrorCreatingInvalidMessage), LazyThreadSafetyMode.None);
            }
            
            sessionTimeout ??= TimeSpan.FromSeconds(10);
            maxPollInterval ??= TimeSpan.FromSeconds(10);
            sweepUncommittedOffsetsInterval ??= TimeSpan.FromSeconds(30);
            readCommittedOffsetsTimeout ??= TimeSpan.FromMilliseconds(5000);
            topicFindTimeout ??= TimeSpan.FromMilliseconds(10000);

            ClientConfig = new ClientConfig
            {
                BootstrapServers = string.Join(",", configuration.BootStrapServers), 
                ClientId = configuration.Name,
                Debug = configuration.Debug,
                SaslMechanism = configuration.SaslMechanisms.HasValue ? (Confluent.Kafka.SaslMechanism?)((int)configuration.SaslMechanisms.Value) : null,
                SaslKerberosPrincipal = configuration.SaslKerberosPrincipal,
                SaslUsername = configuration.SaslUsername,
                SaslPassword = configuration.SaslPassword,
                SecurityProtocol = configuration.SecurityProtocol.HasValue ? (Confluent.Kafka.SecurityProtocol?)((int) configuration.SecurityProtocol.Value) : null,
                SslCaLocation = configuration.SslCaLocation
            };
            
            // We repeat properties because copying them from the ClientConfig modifies the ClientConfig in place 
            _consumerConfig = new ConsumerConfig()
            {
                BootstrapServers = string.Join(",", configuration.BootStrapServers), 
                ClientId = configuration.Name,
                Debug = configuration.Debug,
                SaslMechanism = configuration.SaslMechanisms.HasValue ? (Confluent.Kafka.SaslMechanism?)((int)configuration.SaslMechanisms.Value) : null,
                SaslKerberosPrincipal = configuration.SaslKerberosPrincipal,
                SaslUsername = configuration.SaslUsername,
                SaslPassword = configuration.SaslPassword,
                SecurityProtocol = configuration.SecurityProtocol.HasValue ? (Confluent.Kafka.SecurityProtocol?)((int) configuration.SecurityProtocol.Value) : null,
                SslCaLocation = configuration.SslCaLocation,
                GroupId = groupId,
                AutoOffsetReset = offsetDefault,
                SessionTimeoutMs = Convert.ToInt32(sessionTimeout.Value.TotalMilliseconds),
                MaxPollIntervalMs = Convert.ToInt32(maxPollInterval.Value.TotalMilliseconds),
                EnablePartitionEof = true,
                AllowAutoCreateTopics = false, //We will do this explicit always so as to allow us to set parameters for the topic
                IsolationLevel = isolationLevel,
                //We commit the last offset for acknowledged requests when a batch of records has been processed. 
                EnableAutoOffsetStore = false,
                EnableAutoCommit = false,
                // https://www.confluent.io/blog/cooperative-rebalancing-in-kafka-streams-consumer-ksqldb/
                PartitionAssignmentStrategy = partitionAssignmentStrategy,
            };
            
            if (configHook != null)
                configHook(_consumerConfig);

            _maxBatchSize = commitBatchSize;
            _sweepUncommittedInterval = sweepUncommittedOffsetsInterval.Value;
            _readCommittedOffsetsTimeout = readCommittedOffsetsTimeout.Value;

            timeProvider ??= TimeProvider.System;
            _timeProvider = timeProvider;
            _lastFlushAt = _timeProvider.GetUtcNow().UtcDateTime;
            _sweeperTimer = timeProvider.CreateTimer(_ =>
            {
                if (_isClosed)
                {
                    return;
                }
                
                SweepOffsets();
            }, null, _sweepUncommittedInterval, _sweepUncommittedInterval);

            _consumer = new ConsumerBuilder<string, byte[]>(_consumerConfig)
                .SetPartitionsAssignedHandler((_, list) =>
                {
                    var partitions = list.Select(p => $"{p.Topic} : {p.Partition.Value}");
                    
                    Log.PartitionAdded(s_logger, String.Join(",", partitions));
                    
                    _partitions.AddRange(list);
                })
                .SetPartitionsRevokedHandler((_, list) =>
                {
                    //We should commit any offsets we have stored for these partitions
                    CommitOffsetsFor(list);
                    
                    var revokedPartitionInfo = list.Select(tpo => $"{tpo.Topic} : {tpo.Partition}").ToList();
                    
                    Log.PartitionsRevoked(s_logger, string.Join(",", revokedPartitionInfo));
                    
                    _partitions = _partitions.Where(tp => list.All(tpo => tpo.TopicPartition != tp)).ToList();
                })
                .SetPartitionsLostHandler((_, list) =>
                {
                    var lostPartitions = list.Select(tpo => $"{tpo.Topic} : {tpo.Partition}").ToList();
                    
                    Log.PartitionsLost(s_logger, string.Join(",", lostPartitions));
                    
                    _partitions = _partitions.Where(tp => list.All(tpo => tpo.TopicPartition != tp)).ToList();
                })
                .SetErrorHandler((_, error) =>
                {
                    _hasFatalError = error.IsFatal;
                    
                    if (_hasFatalError ) 
                        Log.FatalError(s_logger, error.Code, error.Reason, true);
                    else
                        Log.NonFatalError(s_logger, error.Code, error.Reason, false);
                })
                .Build();

            Log.SubscribingToTopic(s_logger, Topic);
            _consumer.Subscribe([Topic.Value]);

            _creator = new KafkaMessageCreator();
            
            MakeChannels = makeChannels;
            Topic = routingKey;
            NumPartitions = numPartitions;
            ReplicationFactor = replicationFactor;
            TopicFindTimeout = topicFindTimeout.Value;
            
            EnsureTopic();
        }

        /// <summary>
        /// Destroys the consumer
        /// </summary>
        ~KafkaMessageConsumer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <remarks>
        /// We do not have autocommit on and this stores the message that has just been processed.
        /// We use the header bag to store the partition offset of the message when  reading it from Kafka. This enables us to get hold of it when
        /// we acknowledge the message via Brighter. We store the offset via the consumer, and keep an in-memory list of offsets. If we have hit the
        /// batch size we commit the offsets. if not, we trigger the sweeper, which will commit the offset once the specified time interval has passed if
        /// a batch has not done so.
        /// </remarks>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            if (!message.Header.Bag.TryGetValue(HeaderNames.PARTITION_OFFSET, out var bagData))
                    return;
            
            try
            {
                var topicPartitionOffset = bagData as TopicPartitionOffset;
                if (topicPartitionOffset == null)
                {
                    Log.CannotAcknowledgeMessage(s_logger, message.Id);
                    return;
                }

                var offset = new TopicPartitionOffset(topicPartitionOffset.TopicPartition, new Offset(topicPartitionOffset.Offset + 1));

                Log.StoringOffset(s_logger, new Offset(topicPartitionOffset.Offset + 1).Value, topicPartitionOffset.TopicPartition.Topic, topicPartitionOffset.TopicPartition.Partition.Value);
                _offsetStorage.Add(offset);

                if (_offsetStorage.Count % _maxBatchSize == 0)
                    FlushOffsets();

                Log.CurrentKafkaBatchCount(s_logger, _offsetStorage.Count.ToString(), _maxBatchSize.ToString());
            }
            catch (TopicPartitionException tpe)
            {
                var results = tpe.Results.Select(r =>
                    $"Error committing topic {r.Topic} for partition {r.Partition.Value.ToString()} because {r.Error.Reason}");
                var errorString = string.Join(Environment.NewLine, results);
                Log.ErrorCommittingOffsetsAsDebug(s_logger, errorString);
            }
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <remarks>
        /// We do not have autocommit on and this stores the message that has just been processed.
        /// We use the header bag to store the partition offset of the message when  reading it from Kafka. This enables us to get hold of it when
        /// we acknowledge the message via Brighter. We store the offset via the consumer, and keep an in-memory list of offsets. If we have hit the
        /// batch size we commit the offsets. if not, we trigger the sweeper, which will commit the offset once the specified time interval has passed if
        /// a batch has not done so.
        /// This just calls the sync method, which does not block
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">A cancellation token - not used as calls the sync method which does not block</param>
        public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
        {
            Acknowledge(message);
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Nacks the specified message. For Kafka (stream-based), this is a no-op because not committing
        /// the offset is sufficient to allow redelivery.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Nack(Message message)
        {
            // No-op for Kafka: not committing the offset is sufficient for redelivery
        }

        /// <summary>
        /// Nacks the specified message. For Kafka (stream-based), this is a no-op because not committing
        /// the offset is sufficient to allow redelivery.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">Cancel the nack operation</param>
        public Task NackAsync(Message message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Close the consumer
        /// - Commit any outstanding offsets
        /// - Surrender any assignments
        /// </summary>
        /// <remarks>Use this before disposing of the consumer, to ensure an orderly shutdown</remarks>
        public void Close()
        {
            //we will be called twice if explicitly disposed as well as closed, so just skip in that case
            if (_isClosed) return;
            
            try
            {
                _flushToken.Wait(TimeSpan.Zero);
                //this will release the semaphore
               CommitAllOffsets(_timeProvider.GetUtcNow().UtcDateTime);
            }
            catch (Exception ex)
            {
                //Close anyway, we just will get replay of those messages
                Log.ErrorCommittingOffsetBeforeClosing(s_logger, ex.Message);
            }
            finally
            {
                _consumer.Close();
                _isClosed = true;
            }
        }

       /// <summary>
       /// Purges the specified queue name.
       /// </summary>
       /// <remarks>
       /// There is no 'queue' to purge in Kafka, so we treat this as moving past to the offset to tne end of any assigned partitions,
       /// thus skipping over anything that exists at that point.
       /// </remarks>
        public void Purge()
        {
            if (!_consumer.Assignment.Any())
                return;

            foreach (var topicPartition in _consumer.Assignment)
            {
                _consumer.Seek(new TopicPartitionOffset(topicPartition, Offset.End));
            }
        }
       
        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        /// <remarks>
        /// There is no 'queue' to purge in Kafka, so we treat this as moving past to the offset to tne end of any assigned partitions,
        /// thus skipping over anything that exists at that point.
        /// As the Confluent library does not support async, this is sync over async and would block the main performer thread
        /// so we use a new thread pool thread to run this and await that. This could lead to thread pool exhaustion but Purge is rarely used
        /// in production code
        /// </remarks>
       /// <param name="cancellationToken"></param>
        public async Task PurgeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            var purgeTask = Task.Run(() => 
            {
                try
                {
                    Purge();
                    tcs.SetResult(new object());
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }, cancellationToken);
            
            await tcs.Task;
            await purgeTask;
        }

        /// <summary>
        /// Receives from the specified topic. Used by a <see cref="Channel"/> to provide access to the stream.
        /// </summary>
        /// <remarks>
        /// We consume the next offset from the stream, and turn it into a Brighter message; we store the offset in the partition into the Brighter message
        /// headers for use in storing and committing offsets. If the stream is EOF or we are not allocated partitions, returns an empty message. 
        /// </remarks>
        /// <param name="timeOut">The timeout for receiving a message. Defaults to 300ms</param>
        /// <returns>A Brighter message wrapping the payload from the Kafka stream</returns>
        /// <exception cref="ChannelFailureException">We catch Kafka consumer errors and rethrow as a ChannelFailureException </exception>
        public Message[] Receive(TimeSpan? timeOut = null)
        {
            if (_hasFatalError)
                throw new ChannelFailureException("Fatal error on Kafka consumer, see logs for details");
            
            timeOut ??= TimeSpan.FromMilliseconds(300);
            
            try
            {
                
                LogOffSets();

                Log.ConsumingMessages(s_logger, timeOut.Value.TotalMilliseconds);
                var consumeResult = _consumer.Consume(timeOut.Value);

                if (consumeResult == null)
                {
                    CheckHasPartitions();
                    
                    Log.NoMessagesAvailable(s_logger);
                    return [new Message()];
                }

                if (consumeResult.IsPartitionEOF)
                {
                    Log.EndOfPartition(s_logger, _consumer.MemberId);
                    return [new Message()];
                }

                Log.UsableMessageRetrieved(s_logger, consumeResult.Message.Value);
                Log.PartitionOffsetValue(s_logger, consumeResult.Partition, consumeResult.Offset, consumeResult.Message.Value);

                return [_creator.CreateMessage(consumeResult)];
            }
            catch (ConsumeException consumeException)
            {
                Log.ErrorListeningToTopic(s_logger, consumeException, Topic ?? RoutingKey.Empty, _consumerConfig.GroupId, _consumerConfig.BootstrapServers);
                throw new ChannelFailureException("Error connecting to Kafka, see inner exception for details", consumeException);

            }
            catch (KafkaException kafkaException)
            {
                Log.ErrorListeningToTopic(s_logger, kafkaException, Topic ?? RoutingKey.Empty, _consumerConfig.GroupId, _consumerConfig.BootstrapServers);
                if (kafkaException.Error.IsFatal) //this can't be recovered and requires a new consumer
                    throw;
                
                throw new ChannelFailureException("Error connecting to Kafka, see inner exception for details", kafkaException);
            }
            catch (Exception exception)
            {
                Log.ErrorListeningToTopic(s_logger, exception, Topic ?? RoutingKey.Empty, _consumerConfig.GroupId, _consumerConfig.BootstrapServers);
                throw;
            }
        }

        /// <summary>
        /// Receives from the specified topic. Used by a <see cref="Channel"/> to provide access to the stream.
        /// </summary>
        /// <remarks>
        /// We consume the next offset from the stream, and turn it into a Brighter message; we store the offset in the partition into the Brighter message
        /// headers for use in storing and committing offsets. If the stream is EOF or we are not allocated partitions, returns an empty message.
        /// Kafka does not support an async consumer, and probably never will. See <a href="https://github.com/confluentinc/confluent-kafka-dotnet/issues/487">Confluent Kafka</a>
        /// As a result we use TimeSpan.Zero to run the recieve loop, which will stop it blocking
        /// </remarks>
        /// <param name="timeOut">The timeout for receiving a message. For async always treated as zero</param>
        /// <param name="cancellationToken">The cancellation token - not used as this is async over sync</param>
        /// <returns>A Brighter message wrapping the payload from the Kafka stream</returns>
        /// <exception cref="ChannelFailureException">We catch Kafka consumer errors and rethrow as a ChannelFailureException </exception>
        public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<Message[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            timeOut ??= TimeSpan.Zero;

            var recieveTask = Task.Run(() => 
            {
                try
                {
                    var messages = Receive(timeOut);
                    tcs.SetResult(messages);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }, cancellationToken);
            
            var messages = await tcs.Task;
            await recieveTask;
            
            return messages;
        }

        /// <summary>
        /// Rejects the specified message. 
        /// </summary>
        /// <remarks>
        /// This is just a commit of the offset to move past the record without processing it
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
        /// <returns>True if the message has been removed from the channel, false otherwise</returns>
        public bool Reject(Message message, MessageRejectionReason? reason = null)
        {
              // If no reason provided or no channels configured, just acknowledge
              if (_deadLetterProducer == null && _invalidMessageProducer == null)
              {
                  if (reason != null)
                  {
                      Log.NoChannelsConfiguredForRejection(s_logger, message.Header.MessageId, reason.RejectionReason.ToString());
                  }
                  Acknowledge(message);
                  return true;
              }

              var rejectionReason = reason?.RejectionReason ?? RejectionReason.None;
              var partitionOffset = ExtractPartitionOffset(message);

              try
              {
                  RefreshMetadata(message, reason);

                  // Determine routing based on rejection reason
                  var (routingKey, shouldRoute, isFallingBackToDlq) = DetermineRejectionRoute(
                      rejectionReason, _invalidMessageProducer != null, _deadLetterProducer != null);

                  IAmAMessageProducerSync? producer = null;
                  if (shouldRoute)
                  {
                      message.Header.Topic = routingKey!;
                      if (isFallingBackToDlq)
                          Log.FallingBackToDLQ(s_logger, message.Header.MessageId);

                      // Get the appropriate producer based on routing
                      producer = GetRejectionProducer(routingKey);
                  }

                  if (producer != null)
                  {
                      producer.Send(message);
                      Log.MessageSentToRejectionChannel(s_logger, message.Header.MessageId, rejectionReason.ToString());
                  }
                  else
                  {
                      Log.NoChannelsConfiguredForRejection(s_logger, message.Header.MessageId, rejectionReason.ToString());
                  }
              }
              catch (Exception ex)
              {
                  Log.ErrorSendingToRejectionChannel(s_logger, ex, message.Header.MessageId, rejectionReason.ToString());
                  Acknowledge(message);
                  return true;
              }

              AcknowledgeOffset(partitionOffset);
              return true;
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <remarks>
        /// Routes rejected messages to dead letter queue or invalid message channel based on rejection reason.
        /// Enriches message with metadata and acknowledges after sending to error channel (or on failure).
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
        /// <param name="cancellationToken">Cancels the reject operation</param>
        public async Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
        {
            // If no reason provided or no channels configured, just acknowledge
            if (_deadLetterProducer == null && _invalidMessageProducer == null)
            {
                if (reason != null)
                {
                    Log.NoChannelsConfiguredForRejection(s_logger, message.Header.MessageId, reason.RejectionReason.ToString());
                }
                await AcknowledgeAsync(message, cancellationToken);
                return true;
            }

            var rejectionReason = reason?.RejectionReason ?? RejectionReason.None;
            var partitionOffset = ExtractPartitionOffset(message);

            try
            {
                RefreshMetadata(message, reason);

                // Determine routing based on rejection reason
                var (routingKey, shouldRoute, isFallingBackToDlq) = DetermineRejectionRoute(
                    rejectionReason, _invalidMessageProducer != null, _deadLetterProducer != null);

                IAmAMessageProducerAsync? producer = null;
                if (shouldRoute)
                {
                    message.Header.Topic = routingKey!;
                    if (isFallingBackToDlq)
                        Log.FallingBackToDLQ(s_logger, message.Header.MessageId);

                    // Get the appropriate producer based on routing
                    producer = GetRejectionProducer(routingKey);
                }

                if (producer != null)
                {
                    await producer.SendAsync(message, cancellationToken);
                    Log.MessageSentToRejectionChannel(s_logger, message.Header.MessageId, rejectionReason.ToString());
                }
                else
                {
                    Log.NoChannelsConfiguredForRejection(s_logger, message.Header.MessageId, rejectionReason.ToString());
                }
            }
            catch (Exception ex)
            {
                Log.ErrorSendingToRejectionChannel(s_logger, ex, message.Header.MessageId, rejectionReason.ToString());
                await AcknowledgeAsync(message, cancellationToken);
                return true;
            }

            AcknowledgeOffset(partitionOffset);
            return true;
        }

        /// <summary>
        /// Requeues the specified message. Kafka streams are immutable, so requeue publishes a new
        /// message to the same topic via a lazily-created producer. When a delay is specified and a
        /// scheduler is configured, the producer delegates to the scheduler for delayed redelivery.
        /// For immediate requeue (zero or null delay) the message is produced directly and flushed.
        /// </summary>
        /// <param name="message">The message to requeue.</param>
        /// <param name="delay">Delay before the message should be redelivered.</param>
        /// <returns>True if the message was successfully requeued.</returns>
        public bool Requeue(Message message, TimeSpan? delay = null)
        {
            delay ??= TimeSpan.Zero;
            var partitionOffset = ExtractPartitionOffset(message);
            CleanBagForResend(message);

            if (delay > TimeSpan.Zero)
            {
                EnsureRequeueProducer();
                _requeueProducer!.SendWithDelay(message, delay);
            }
            else
            {
                // Kafka streams are immutable — immediate requeue must produce a new message
                EnsureRequeueProducer();
                _requeueProducer!.Send(message);
                _requeueProducer.Flush();
            }

            AcknowledgeOffset(partitionOffset);
            return true;
        }

        /// <summary>
        /// Requeues the specified message. Kafka streams are immutable, so requeue publishes a new
        /// message to the same topic via a lazily-created producer. When a delay is specified and a
        /// scheduler is configured, the producer delegates to the scheduler for delayed redelivery.
        /// For immediate requeue (zero or null delay) the message is produced directly and flushed.
        /// </summary>
        /// <param name="message">The message to requeue.</param>
        /// <param name="delay">Delay before the message should be redelivered.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>True if the message was successfully requeued.</returns>
        public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            delay ??= TimeSpan.Zero;
            var partitionOffset = ExtractPartitionOffset(message);
            CleanBagForResend(message);

            if (delay > TimeSpan.Zero)
            {
                EnsureRequeueProducer();
                await _requeueProducer!.SendWithDelayAsync(message, delay, cancellationToken);
            }
            else
            {
                // Kafka streams are immutable — immediate requeue must produce a new message
                EnsureRequeueProducer();
                await _requeueProducer!.SendAsync(message, cancellationToken);
                _requeueProducer.Flush(cancellationToken);
            }

            AcknowledgeOffset(partitionOffset);
            return true;
        }
        
        private void CheckHasPartitions()
        {
            if (_partitions.Count <= 0)
                Log.NoPartitionsAllocated(s_logger);
        }


        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        private void LogOffSets()
        {
            try
            {
                var highestReadOffset = new Dictionary<TopicPartition, long>();
            
                var committedOffsets = _consumer.Committed(_partitions, _readCommittedOffsetsTimeout);
                foreach (var committedOffset in committedOffsets)
                {
                    if (highestReadOffset.TryGetValue(committedOffset.TopicPartition, out long offset))
                    {
                        if (committedOffset.Offset < offset) continue;
                    }
                    highestReadOffset[committedOffset.TopicPartition] = committedOffset.Offset;
                }

                foreach (KeyValuePair<TopicPartition,long> pair in highestReadOffset)
                {
                    var topicPartition = pair.Key;
                    Log.OffsetToConsumeFrom(s_logger, pair.Value.ToString(), topicPartition.Partition.Value.ToString(), topicPartition.Topic);
                }
            }
            catch (KafkaException ke)
            {
                //This is only login for debug, so skip errors here
                Log.KafkaErrorLoggingOffsets(s_logger, ke.Message);
            }
        }

        /// <summary>
        /// Mainly used diagnostically in tests - how many offsets do we have now?
        /// </summary>
        /// <returns></returns>
        public int StoredOffsets()
        {
            return _offsetStorage.Count;
        }
        
        /// <summary>
        /// We commit a batch size worth at a time; this may be called from the sweeper thread, and we don't want it to
        /// loop endlessly over the offset list as new items are added, which will trigger a commit anyway. So we limit
        /// the trigger to only commit a batch size worth
        /// </summary>
        private void CommitOffsets()
        {
            try
            {
                var listOffsets = new List<TopicPartitionOffset>();
                for (int i = 0; i < _maxBatchSize; i++)
                {
                    bool hasOffsets = _offsetStorage.TryTake(out var offset);
                    if (hasOffsets)
                        listOffsets.Add(offset!);
                    else
                        break;

                }

                if (s_logger.IsEnabled(LogLevel.Information))
                {
                    var offsets = listOffsets.Select(tpo =>
                        $"Topic: {tpo.Topic} Partition: {tpo.Partition.Value} Offset: {tpo.Offset.Value}");
                    var offsetAsString = string.Join(Environment.NewLine, offsets);
                    Log.CommittingOffsets(s_logger, Environment.NewLine, offsetAsString);
                }

                _consumer.Commit(listOffsets);
            }
            catch(Exception ex)
            {
                //may happen if the consumer is not valid when the thread runs
                Log.ErrorCommittingOffsetsAsWarning(s_logger, ex.Message);
            }
            finally
            {
                _flushToken.Release(1);
            }
        }

        //Called during a revoke, we are passed the partitions that we are revoking and their last offset and we need to
        //commit anything we have not stored.
        private void CommitOffsetsFor(List<TopicPartitionOffset> revokedPartitions)
        {
            try
            {
                //find the provided set of partitions amongst our stored offsets 
                var partitionOffsets = _offsetStorage.ToArray();
                var revokedOffsetsToCommit =
                    partitionOffsets.Where(tpo =>
                            revokedPartitions.Any(ptc =>
                                ptc.TopicPartition == tpo.TopicPartition 
                                && ptc.Offset.Value != Offset.Unset.Value 
                                && tpo.Offset.Value > ptc.Offset.Value 
                            )
                        )
                        .ToList();
                //determine if we have offsets still to commit
                if (revokedOffsetsToCommit.Any())
                {
                    //commit them
                    LogOffSetCommitRevokedPartitions(revokedOffsetsToCommit);
                    _consumer.Commit(revokedOffsetsToCommit);
                }
            }
            catch (KafkaException error)
            {
                Log.ErrorCommittingOffsetsDuringPartitionRevoke(s_logger, error.Message, error.Error.Code, error.Error.Reason, error.Error.IsFatal);
            }
        }

        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        private void LogOffSetCommitRevokedPartitions(List<TopicPartitionOffset> revokedOffsetsToCommit)
        {
            Log.SavingRevokedPartitionOffsets(s_logger, revokedOffsetsToCommit.Count);
            foreach (var offset in revokedOffsetsToCommit)
            {
                Log.SavingRevokedPartitionOffset(s_logger, offset.Offset.Value.ToString(), offset.Partition.Value.ToString(), offset.Topic);
            }
        }
        
        //Just flush everything
        private void CommitAllOffsets(DateTime flushTime)
        {
            try
            {
                var listOffsets = new List<TopicPartitionOffset>();
                var currentOffsetsInBag = _offsetStorage.Count;
                for (int i = 0; i < currentOffsetsInBag; i++)
                {
                    bool hasOffsets = _offsetStorage.TryTake(out var offset);
                    if (hasOffsets)
                        listOffsets.Add(offset!);
                    else
                        break;

                }

                if (s_logger.IsEnabled(LogLevel.Information) && listOffsets.Count != 0)
                {
                    var offsets = listOffsets.Select(tpo =>
                        $"Topic: {tpo.Topic} Partition: {tpo.Partition.Value} Offset: {tpo.Offset.Value}");
                    var offsetAsString = string.Join(Environment.NewLine, offsets);
                    Log.SweepingOffsets(s_logger, Environment.NewLine, offsetAsString);
                }

                _consumer.Commit(listOffsets);
                _lastFlushAt = flushTime;
            }
            finally
            {
                _flushToken.Release(1);
            }
        }
        
        private void EnsureRequeueProducer()
        {
            LazyInitializer.EnsureInitialized(ref _requeueProducer, ref _requeueProducerInitialized,
                ref _requeueProducerLock, () =>
                {
                    if (Topic is null)
                        throw new ConfigurationException("Cannot create requeue producer: Topic is not configured.");

                    var producer = CreateProducer(Topic)
                        ?? throw new ConfigurationException(
                            $"Failed to create requeue producer for topic {Topic}. Check broker connectivity and configuration.");
                    producer.Scheduler = _scheduler;
                    return producer;
                });
        }

        private KafkaMessageProducer? CreateProducer(RoutingKey topic, Action<ILogger, Exception>? logError = null)
        {
            var publication = new KafkaPublication
            {
                Topic = topic,
                NumPartitions = NumPartitions,
                ReplicationFactor = ReplicationFactor,
                MessageTimeoutMs = 2000,
                RequestTimeoutMs = 2000,
                MakeChannels = MakeChannels
            };

            try
            {
                var producer = new KafkaMessageProducer(_configuration, publication);
                producer.Init();
                return producer;
            }
            catch (Exception e)
            {
                logError?.Invoke(s_logger, e);
                return null;
            }
        }

        private KafkaMessageProducer? GetRejectionProducer(RoutingKey? routingKey)
        {
            if (routingKey == _invalidMessageRoutingKey)
                return _invalidMessageProducer?.Value;
            if (routingKey == _deadLetterRoutingKey)
                return _deadLetterProducer?.Value;
            return null;
        }
        
        /// <summary>
        /// Extracts the <see cref="TopicPartitionOffset"/> from the message bag before it is cleaned for resend.
        /// </summary>
        private static TopicPartitionOffset? ExtractPartitionOffset(Message message)
        {
            if (message.Header.Bag.TryGetValue(HeaderNames.PARTITION_OFFSET, out var bagData))
                return bagData as TopicPartitionOffset;
            return null;
        }

        /// <summary>
        /// Commits a previously extracted partition offset. Use this after <see cref="CleanBagForResend"/>
        /// has removed the offset from the message bag.
        /// </summary>
        private void AcknowledgeOffset(TopicPartitionOffset? partitionOffset)
        {
            if (partitionOffset == null) return;

            var offset = new TopicPartitionOffset(partitionOffset.TopicPartition, new Offset(partitionOffset.Offset + 1));

            Log.StoringOffset(s_logger, new Offset(partitionOffset.Offset + 1).Value, partitionOffset.TopicPartition.Topic, partitionOffset.TopicPartition.Partition.Value);
            _offsetStorage.Add(offset);

            if (_offsetStorage.Count % _maxBatchSize == 0)
                FlushOffsets();
        }

        private static void CleanBagForResend(Message message)
        {
            //remove headers that will be reset by send as set from message properties
            message.Header.Bag.Remove(HeaderNames.PARTITION_OFFSET);
            message.Header.Bag.Remove(HeaderNames.CLOUD_EVENTS_ID);
            message.Header.Bag.Remove(HeaderNames.CLOUD_EVENTS_SPEC_VERSION);
            message.Header.Bag.Remove(HeaderNames.CLOUD_EVENTS_TYPE);
            message.Header.Bag.Remove(HeaderNames.CLOUD_EVENTS_SOURCE);
            message.Header.Bag.Remove(HeaderNames.CLOUD_EVENTS_TIME);
            message.Header.Bag.Remove(HeaderNames.CLOUD_EVENTS_SUBJECT);
            message.Header.Bag.Remove(HeaderNames.CLOUD_EVENTS_DATA_SCHEMA);
            message.Header.Bag.Remove(HeaderNames.CLOUD_EVENTS_TRACE_PARENT);
            message.Header.Bag.Remove(HeaderNames.CLOUD_EVENTS_TRACE_STATE);
            message.Header.Bag.Remove(HeaderNames.W3C_BAGGAGE);
            message.Header.Bag.Remove(HeaderNames.CLOUD_EVENTS_DATA_CONTENT_TYPE);
        }

        private void RefreshMetadata(Message message, MessageRejectionReason? reason)
        {
            // Add rejection metadata
            message.Header.Bag[HeaderNames.ORIGINAL_TOPIC] = message.Header.Topic.Value;
            message.Header.Bag[HeaderNames.REJECTION_TIMESTAMP] = _timeProvider.GetUtcNow().ToString("o");
            message.Header.Bag[HeaderNames.ORIGINAL_TYPE] = message.Header.MessageType.ToString();

            CleanBagForResend(message);

            if (reason == null) return;

            message.Header.Bag[HeaderNames.REJECTION_REASON] = reason.RejectionReason.ToString();
            if (!string.IsNullOrEmpty(reason.Description))
            {
                message.Header.Bag[HeaderNames.REJECTION_MESSAGE] = reason.Description ?? string.Empty;
            }
        }

        /// <summary>
        /// Determines the appropriate routing for a rejected message based on rejection reason and available channels
        /// </summary>
        /// <param name="rejectionReason">The rejection reason</param>
        /// <param name="hasInvalidProducer">Whether an invalid message producer is available</param>
        /// <param name="hasDeadLetterProducer">Whether a dead letter producer is available</param>
        /// <returns>Tuple of (routingKey, foundProducer, isFallingBackToDlq)</returns>
        private (RoutingKey? routingKey, bool foundProducer, bool isFallingBackToDlq) DetermineRejectionRoute(
            RejectionReason rejectionReason,
            bool hasInvalidProducer,
            bool hasDeadLetterProducer)
        {
            
            switch (rejectionReason)
            {
                case RejectionReason.Unacceptable:
                    // Try invalid message channel first, fall back to DLQ
                    if (hasInvalidProducer)
                        return (_invalidMessageRoutingKey, true, false);
                    if (hasDeadLetterProducer)
                        return (_deadLetterRoutingKey, true, true);
                    return (null, false, false);

                case RejectionReason.DeliveryError:
                case RejectionReason.None:    
                default:
                    // Send to DLQ
                    if (hasDeadLetterProducer)
                        return (_deadLetterRoutingKey, true, false);
                    return (null, false, false);
            }
        }

        // The batch size has been exceeded, so flush our offsets
        private void FlushOffsets()
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (_flushToken.Wait(TimeSpan.Zero))
            {
                //This is expensive, so use a background thread
                Task.Factory.StartNew(
                    action: _ => CommitOffsets(),
                    state: now,
                    cancellationToken: CancellationToken.None,
                    creationOptions: TaskCreationOptions.DenyChildAttach,
                    scheduler: TaskScheduler.Default);
            }
            else
            {
                Log.SkippedCommittingOffsets(s_logger);
            }
        }

        //If it is has been too long since we flushed, flush now to prevent offsets accumulating 
        private void SweepOffsets()
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (now - _lastFlushAt < _sweepUncommittedInterval)
            {
                return;
            }
                
            if (_flushToken.Wait(TimeSpan.Zero))
            {
                if (now - _lastFlushAt < _sweepUncommittedInterval)
                {
                    _flushToken.Release(1);
                    return;
                }
                
                //This is expensive, so use a background thread
                Task.Factory.StartNew(
                    action: state => CommitAllOffsets(state is not null ? (DateTime) state : _timeProvider.GetUtcNow().UtcDateTime),
                    state: now,
                    cancellationToken: CancellationToken.None,
                    creationOptions: TaskCreationOptions.DenyChildAttach,
                    scheduler: TaskScheduler.Default);
            }
            else
            {
                Log.SkippedSweepingOffsets(s_logger);
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            _sweeperTimer.Dispose();

            Close();
            _consumer?.Dispose();
            _flushToken?.Dispose();

            // Dispose all producers independently - each may be created without the others
            if (_deadLetterProducer?.IsValueCreated == true)
                _deadLetterProducer.Value?.Dispose();

            if (_invalidMessageProducer?.IsValueCreated == true)
                _invalidMessageProducer.Value?.Dispose();

            _requeueProducer?.Dispose();
        }

        /// <summary>
        /// Disposes of the consumer
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the consumer, async
        /// </summary>
        /// <returns>A value task that manages the disposal</returns>
        public async ValueTask DisposeAsync()
        {
            await _sweeperTimer.DisposeAsync();

            Close();
            _consumer?.Dispose();
            _flushToken?.Dispose();

            if (_deadLetterProducer?.IsValueCreated == true && _deadLetterProducer.Value != null)
                await _deadLetterProducer.Value.DisposeAsync();

            if (_invalidMessageProducer?.IsValueCreated == true && _invalidMessageProducer.Value != null)
                await _invalidMessageProducer.Value.DisposeAsync();

            if (_requeueProducer != null)
                await _requeueProducer.DisposeAsync();

            GC.SuppressFinalize(this);
        }
        
        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "Partition Added {Channels}")]
            public static partial void PartitionAdded(ILogger logger, string channels);
            
            [LoggerMessage(LogLevel.Information, "Partitions for consumer revoked {Channels}")]
            public static partial void PartitionsRevoked(ILogger logger, string channels);
            
            [LoggerMessage(LogLevel.Information, "Partitions for consumer lost {Channels}")]
            public static partial void PartitionsLost(ILogger logger, string channels);
            
            [LoggerMessage(LogLevel.Error, "Code: {ErrorCode}, Reason: {ErrorMessage}, Fatal: {FatalError}")]
            public static partial void FatalError(ILogger logger, ErrorCode errorCode, string errorMessage, bool fatalError);
            
            [LoggerMessage(LogLevel.Warning, "Code: {ErrorCode}, Reason: {ErrorMessage}, Fatal: {FatalError}")]
            public static partial void NonFatalError(ILogger logger, ErrorCode errorCode, string errorMessage, bool fatalError);
            
            [LoggerMessage(LogLevel.Information, "Kafka consumer subscribing to {Topic}")]
            public static partial void SubscribingToTopic(ILogger logger, RoutingKey topic);
            
            [LoggerMessage(LogLevel.Information, "Cannot acknowledge message {MessageId} as no offset data")]
            public static partial void CannotAcknowledgeMessage(ILogger logger, string messageId);
            
            [LoggerMessage(LogLevel.Information, "Storing offset {Offset} to topic {Topic} for partition {ChannelName}")]
            public static partial void StoringOffset(ILogger logger, long offset, string topic, int channelName);
            
            [LoggerMessage(LogLevel.Information, "Current Kafka batch count {OffsetCount} and {MaxBatchSize}")]
            public static partial void CurrentKafkaBatchCount(ILogger logger, string offsetCount, string maxBatchSize);
            
            [LoggerMessage(LogLevel.Debug, "Error committing offsets: {NewLine} {ErrorMessage}")]
            public static partial void ErrorCommittingOffsetsDebug(ILogger logger, string newLine, string errorMessage);
            
            [LoggerMessage(LogLevel.Debug, "Error committing the current offset to Kafka before closing: {ErrorMessage}")]
            public static partial void ErrorCommittingOffsetBeforeClosing(ILogger logger, string errorMessage);
            
            [LoggerMessage(LogLevel.Debug, "Consuming messages from Kafka stream, will wait for {Timeout}")]
            public static partial void ConsumingMessages(ILogger logger, double timeout);
            
            [LoggerMessage(LogLevel.Debug, "No messages available from Kafka stream")]
            public static partial void NoMessagesAvailable(ILogger logger);
            
            [LoggerMessage(LogLevel.Debug, "Consumer {ConsumerMemberId} has reached the end of the partition")]
            public static partial void EndOfPartition(ILogger logger, string consumerMemberId);
            
            [LoggerMessage(LogLevel.Debug, "Usable message retrieved from Kafka stream: {Request}")]
            public static partial void UsableMessageRetrieved(ILogger logger, byte[] request);
            
            [LoggerMessage(LogLevel.Debug, "Partition: {ChannelName} Offset: {Offset} Value: {Request}")]
            public static partial void PartitionOffsetValue(ILogger logger, Partition channelName, Offset offset, byte[] request);
            
            [LoggerMessage(LogLevel.Error, "KafkaMessageConsumer: There was an error listening to topic {Topic} with groupId {ConsumerGroupId} on bootstrap servers: {Servers})")]
            public static partial void ErrorListeningToTopic(ILogger logger, Exception exception, RoutingKey topic, string consumerGroupId, string servers);
            
            [LoggerMessage(LogLevel.Debug, "Consumer is not allocated any partitions")]
            public static partial void NoPartitionsAllocated(ILogger logger);
            
            [LoggerMessage(LogLevel.Debug, "Offset to consume from is: {Offset} on partition: {ChannelName} for topic: {Topic}")]
            public static partial void OffsetToConsumeFrom(ILogger logger, string offset, string channelName, string topic);
            
            [LoggerMessage(LogLevel.Debug, "Kafka error logging offsets: {ErrorMessage}")]
            public static partial void KafkaErrorLoggingOffsets(ILogger logger, string errorMessage);
            
            [LoggerMessage(LogLevel.Information, "Commiting offsets: {NewLine} {Offset}")]
            public static partial void CommittingOffsets(ILogger logger, string newLine, string offset);
            
            [LoggerMessage(LogLevel.Warning, "KafkaMessageConsumer: Error Committing Offsets: {ErrorMessage}")]
            public static partial void ErrorCommittingOffsetsAsWarning(ILogger logger, string errorMessage);

            [LoggerMessage(LogLevel.Debug, "Error Committing Offsets: {ErrorMessage}")]
            public static partial void ErrorCommittingOffsetsAsDebug(ILogger logger, string errorMessage);
            
            [LoggerMessage(LogLevel.Error, "Error Committing Offsets During Partition Revoke: {Message} Code: {ErrorCode}, Reason: {ErrorMessage}, Fatal: {FatalError}")]
            public static partial void ErrorCommittingOffsetsDuringPartitionRevoke(ILogger logger, string message, ErrorCode errorCode, string errorMessage, bool fatalError);
            
            [LoggerMessage(LogLevel.Debug, "Saving revoked partition offsets: {OffsetCount}")]
            public static partial void SavingRevokedPartitionOffsets(ILogger logger, int offsetCount);
            
            [LoggerMessage(LogLevel.Debug, "Saving revoked partition offset: {Offset} on partition: {Partition} for topic: {Topic}")]
            public static partial void SavingRevokedPartitionOffset(ILogger logger, string offset, string partition, string topic);
            
            [LoggerMessage(LogLevel.Information, "Sweeping offsets: {NewLine} {Offset}")]
            public static partial void SweepingOffsets(ILogger logger, string newLine, string offset);
            
            [LoggerMessage(LogLevel.Information, "Skipped committing offsets, as another commit or sweep was running")]
            public static partial void SkippedCommittingOffsets(ILogger logger);
            
            [LoggerMessage(LogLevel.Information, "Skipped sweeping offsets, as another commit or sweep was running")]
            public static partial void SkippedSweepingOffsets(ILogger logger);
            
            [LoggerMessage(LogLevel.Warning, "Message {MessageId} rejected with reason {RejectionReason} but no channels configured for rejection")]
            public static partial void NoChannelsConfiguredForRejection(ILogger logger, string messageId, string rejectionReason);

            [LoggerMessage(LogLevel.Information, "Message {MessageId} falling back to DLQ as invalid message channel not configured")]
            public static partial void FallingBackToDLQ(ILogger logger, string messageId);

            [LoggerMessage(LogLevel.Information, "Message {MessageId} sent to rejection channel with reason {RejectionReason}")]
            public static partial void MessageSentToRejectionChannel(ILogger logger, string messageId, string rejectionReason);

            [LoggerMessage(LogLevel.Error, "Error sending message {MessageId} to rejection channel with reason {RejectionReason}")]
            public static partial void ErrorSendingToRejectionChannel(ILogger logger, Exception exception, string messageId, string rejectionReason);

            [LoggerMessage(LogLevel.Error, "Error Creating DLQ")]
            public static partial void ErrorCreatingDLQ(ILogger logger, Exception exception);

            [LoggerMessage(LogLevel.Error, "Error Creating Invalid Message Channel")]
            public static partial void ErrorCreatingInvalidMessage(ILogger logger, Exception exception);
        }
    }
}
