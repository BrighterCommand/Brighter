using System;
using System.Diagnostics;
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
    /// Simple Brighter message producer using DotPulsar.
    /// Sends messages as byte[] encoded in UTF-8.
    /// </summary>
    public sealed class PulsarMessageProducer : IAmAMessageProducer, IAsyncDisposable
    {
        private readonly PulsarMessagingGatewayConfiguration _config;
        private readonly IPulsarClient _client;
        private IProducer<byte[]>? _producer;

        // IAmAMessageProducer contract
        public Publication Publication { get; }
        public Activity? Span { get; set; }
        public IAmAMessageScheduler? Scheduler { get; set; }

        public PulsarMessageProducer(PulsarMessagingGatewayConfiguration config, Publication publication)
            : this(config, publication, PulsarClient.Builder().ServiceUrl(new Uri(config.ServiceUrl)).Build())
        { }

        public PulsarMessageProducer(PulsarMessagingGatewayConfiguration config, Publication publication, IPulsarClient client)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _config.Validate();
            Publication = publication ?? throw new ArgumentNullException(nameof(publication));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Ensure the Pulsar producer is created only once.
        /// </summary>
        private void EnsureProducer()
        {
            if (_producer != null)
                return;

            _producer = _client
                .NewProducer(Schema.ByteArray)
                .Topic(_config.Topic)
                .Create();
        }

        /// <summary>
        /// Send a Brighter Message to Pulsar.
        /// IMPORTANT: Brighter will wrap this through its own pipeline,
        /// so this method focuses purely on encoding and publishing.
        /// </summary>
        public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            EnsureProducer();

            // Convert message body to UTF-8 byte[]
            var payload = Encoding.UTF8.GetBytes(message.Body.Value);

            // DotPulsar's Send sends a single message and waits for acknowledgment from Pulsar
            await _producer!.Send(payload, cancellationToken);
        }

        /// <summary>
        /// Dispose Pulsar producer and client gracefully.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_producer is not null)
                    await _producer.DisposeAsync();
            }
            finally
            {
                await _client.DisposeAsync();
            }
        }
    }
}
