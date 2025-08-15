using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using Paramore.Brighter;

namespace Paramore.Brighter.MessagingGateway.Pulsar
{
    /// <summary>
    /// Brighter message consumer implementation using DotPulsar.
    /// Responsible for receiving, acknowledging, and requeuing messages from Pulsar.
    /// </summary>
    public sealed class PulsarMessageConsumer : IAmAMessageConsumerAsync
    {
        private readonly PulsarMessagingGatewayConfiguration _config;
        private readonly IPulsarClient _client;
        private IConsumer<byte[]>? _consumer;

        // IMPORTANT: Map between Brighter's Message.Id -> Pulsar IMessage
        // Needed to ACK or REQUEUE the exact message later.
        private readonly ConcurrentDictionary<Id, IMessage<byte[]>> _inflight = new();

        public PulsarMessageConsumer(PulsarMessagingGatewayConfiguration config)
            : this(config, PulsarClient.Builder().ServiceUrl(new Uri(config.ServiceUrl)).Build())
        { }

        public PulsarMessageConsumer(PulsarMessagingGatewayConfiguration config, IPulsarClient client)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _config.Validate();
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Ensure that the Pulsar consumer is created only once.
        /// </summary>
        private void EnsureConsumer()
        {
            if (_consumer != null)
                return;

            _consumer = _client
                .NewConsumer(Schema.ByteArray)
                .Topic(_config.Topic)
                .SubscriptionName(_config.SubscriptionName)
                .Create();
        }

        /// <summary>
        /// Receive a single message from Pulsar and return it as a Brighter Message.
        /// Note: Brighter's channel.Receive() often works in small batches; returning one message here is sufficient.
        /// </summary>
        public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
        {
            EnsureConsumer();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeOut ?? TimeSpan.FromMilliseconds(1000));

            try
            {
                await foreach (var pulsarMsg in _consumer!.Messages(cts.Token))
                {
                    // Convert byte[] payload -> UTF8 string body
                    var payload = pulsarMsg.Data.ToArray();
                    var bodyText = Encoding.UTF8.GetString(payload);

                    // Create a Brighter message with a new unique Id
                    var brighterId = new Id(Guid.NewGuid().ToString());
                    var brighterMsg = new Message(
                        new MessageHeader(brighterId, _config.Topic, MessageType.MT_EVENT),
                        new MessageBody(bodyText)
                    );

                    // Track message for later ACK/REQUEUE
                    _inflight[brighterId] = pulsarMsg;

                    return new[] { brighterMsg };
                }
            }
            catch (OperationCanceledException)
            {
                // IMPORTANT: Timeout reached -> return empty array (no messages)
            }
            catch (Exception ex)
            {
                // TODO: Replace Console.Error with ILogger
                Console.Error.WriteLine($"[Pulsar] Receive error: {ex}");
            }

            return Array.Empty<Message>();
        }

        /// <summary>
        /// Acknowledge a previously received message.
        /// </summary>
        public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (_consumer == null)
                return;

            if (_inflight.TryRemove(message.Id, out var pulsarMsg))
            {
                // DotPulsar.Extensions – ACK must be called with the exact IMessage received.
                await _consumer.Acknowledge(pulsarMsg, cancellationToken);
            }
        }

        /// <summary>
        /// Reject is not natively supported by Pulsar; we simulate by requesting re-delivery.
        /// </summary>
        public async Task<bool> RejectAsync(Message message, CancellationToken cancellationToken = default)
        {
            return await RequeueAsync(message, null, cancellationToken);
        }

        /// <summary>
        /// Pulsar does not support purging messages directly for a topic/subscription.
        /// </summary>
        public async Task PurgeAsync(CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new NotSupportedException("Apache Pulsar does not support purging messages directly.");
        }

        /// <summary>
        /// Request re-delivery for a specific unacknowledged message.
        /// NOTE: Pulsar will redeliver; delay parameter is ignored (not supported natively).
        /// </summary>
        public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (_consumer == null)
                return false;

            if (_inflight.TryRemove(message.Id, out var pulsarMsg))
            {
                // IMPORTANT: Correct way to ask Pulsar to redeliver a specific message.
                await _consumer.RedeliverUnacknowledgedMessages(new[] { pulsarMsg.MessageId }, cancellationToken);
                return true;
            }

            // If message is not in _inflight, we cannot requeue it specifically.
            return false;
        }

        /// <summary>
        /// Dispose Pulsar consumer and client gracefully.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_consumer is not null)
                    await _consumer.DisposeAsync();
            }
            finally
            {
                await _client.DisposeAsync();
            }
        }
    }
}
