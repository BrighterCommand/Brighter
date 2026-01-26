using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// C
    /// The <see cref="MqttMessageProducer"/> is used by a client to talk to a server and abstracts the infrastructure for inter-process communication away from clients.
    /// It handles subscription establishment, request sending and error handling
    /// </summary>
    public partial class MqttMessageProducer : IAmAMessageProducerAsync, IAmAMessageProducerSync
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MqttMessageProducer>();

        private readonly MqttMessagePublisher _mqttMessagePublisher;

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttMessageProducer" /> class.
        /// </summary>
        /// <param name="mqttMessagePublisher">The publisher used to send messages</param>
        /// <param name="publication">The <see cref="Publication"/> for this producer</param>
        public MqttMessageProducer(MqttMessagePublisher mqttMessagePublisher, Publication publication)
        {
            _mqttMessagePublisher = mqttMessagePublisher;
            Publication = publication;
        }

        /// <summary>
        /// Gets or sets the maximum number of outstanding messages that can be sent without acknowledgment.
        /// A value of -1 indicates no limit on the number of outstanding messages.
        /// </summary>
        public int MaxOutStandingMessages { get; set; } = -1;

        /// <summary>
        /// Gets or sets the interval, in milliseconds, at which the producer checks for outstanding messages.
        /// </summary>
        /// <remarks>
        /// This property determines the frequency of checks for outstanding messages when the producer is managing message flow.
        /// A value of <c>0</c> disables the periodic checks.
        /// </remarks>
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; } = 0;

        /// <summary>
        /// Gets or sets a collection of key-value pairs that can be used to store additional metadata or context 
        /// related to the messages being produced.
        /// </summary>
        /// <remarks>
        /// This property serves as a flexible container for storing custom data that may be required during 
        /// message production or processing. The keys are strings, and the values are objects, allowing for 
        /// diverse types of data to be stored.
        /// </remarks>
        public Dictionary<string, object> OutBoxBag { get; set; } = [];

        /// <inheritdoc />
        public Publication Publication { get; set; }

        /// <inheritdoc />
        public Activity? Span { get; set; }

        /// <inheritdoc />
        public IAmAMessageScheduler? Scheduler { get; set; }

        /// <summary>
        /// Disposes of the producer
        /// </summary>
        public void Dispose() { }

        /// <summary>
        /// Disposes of the producer
        /// </summary>
        /// <returns></returns>
        public ValueTask DisposeAsync()
        {
            return new ValueTask(Task.CompletedTask);
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Send(Message message)
        {
            SendWithDelay(message, TimeSpan.Zero);
        }

        /// <summary>
        /// Sends the specified message asynchronously.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">Allows cancellation of the send operation</param>
        /// <returns>Task.</returns>
        public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
        {
            await SendWithDelayAsync(message, TimeSpan.Zero, cancellationToken);
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delay">Delay is not natively supported - don't block with Task.Delay</param>
        public void SendWithDelay(Message message, TimeSpan? delay = null)
        {
            delay ??= TimeSpan.Zero;
            if (delay != TimeSpan.Zero)
            {
                var schedulerSync = (IAmAMessageSchedulerSync)Scheduler!;
                schedulerSync.Schedule(message, delay.Value);
                return;
            }

            // delay is not natively supported
            ArgumentNullException.ThrowIfNull(message);

            _mqttMessagePublisher.PublishMessage(message);
        }

        /// <summary>
        /// Sens the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delay">Delay is not natively supported - don't block with Task.Delay</param>
        /// <param name="cancellationToken">Allows cancellation of the Send operation</param>
        public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        {
            delay ??= TimeSpan.Zero;
            if (delay != TimeSpan.Zero)
            {
                var schedulerAsync = (IAmAMessageSchedulerAsync)Scheduler!;
                await schedulerAsync.ScheduleAsync(message, delay.Value, cancellationToken);
                return;
            }

            ArgumentNullException.ThrowIfNull(message);

            await _mqttMessagePublisher.PublishMessageAsync(message, cancellationToken);
        }
    }
}
