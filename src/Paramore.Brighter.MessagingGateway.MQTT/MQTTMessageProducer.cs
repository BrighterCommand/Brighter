using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// Class ClientRequestHandler .
    /// The <see cref="MQTTMessageProducer"/> is used by a client to talk to a server and abstracts the infrastructure for inter-process communication away from clients.
    /// It handles subscription establishment, request sending and error handling
    /// </summary>
    public class MQTTMessageProducer : IAmAMessageProducer, IAmAMessageProducerAsync, IAmAMessageProducerSync
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MQTTMessageProducer>();
        
        public int MaxOutStandingMessages { get; set; } = -1;
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; } = 0;
        public Dictionary<string, object> OutBoxBag { get; set; } = new Dictionary<string, object>();

        /// <inheritdoc />
        public Publication Publication { get; set; }

        /// <inheritdoc />
        public Activity Span { get; set; }
        
        /// <inheritdoc />
        public IAmAMessageScheduler Scheduler { get; set; }

        private MQTTMessagePublisher _mqttMessagePublisher;

        /// <summary>
        /// Initializes a new instance of the <see cref="MQTTMessageProducer" /> class.
        /// </summary>
        /// <param name="mqttMessagePublisher">The publisher used to send messages</param>
        public MQTTMessageProducer(MQTTMessagePublisher mqttMessagePublisher)
        {
            _mqttMessagePublisher = mqttMessagePublisher;
        }

        /// <summary>
        /// Disposes of the producer
        /// </summary>
        public void Dispose(){ }
        
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
                if (Scheduler is IAmAMessageSchedulerSync sync)
                {
                    sync.Schedule(message, delay.Value);
                    return;
                }
                  
                if (Scheduler is IAmAMessageSchedulerAsync async)
                {
                    BrighterAsyncContext.Run(async () => await async.ScheduleAsync(message, delay.Value));
                    return;
                } 
                  
                s_logger.LogWarning("MQTTMessageProducer: no scheduler configured, message will be sent immediately");
            }
            
            // delay is not natively supported
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            
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
                if (Scheduler is IAmAMessageSchedulerAsync async)
                {
                    await async.ScheduleAsync(message, delay.Value, cancellationToken);
                    return;
                }

                if (Scheduler is IAmAMessageSchedulerSync sync)
                {
                    sync.Schedule(message, delay.Value);
                    return;
                }
                
                s_logger.LogWarning("MQTTMessageProducer: no scheduler configured, message will be sent immediately");
            }
            
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            await _mqttMessagePublisher.PublishMessageAsync(message, cancellationToken);
        }
    }
}
