using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// Class ClientRequestHandler .
    /// The <see cref="MQTTMessageProducer"/> is used by a client to talk to a server and abstracts the infrastructure for inter-process communication away from clients.
    /// It handles subscription establishment, request sending and error handling
    /// </summary>
    public class MQTTMessageProducer : IAmAMessageProducer, IAmAMessageProducerAsync, IAmAMessageProducerSync
    {
        public int MaxOutStandingMessages { get; set; } = -1;
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; } = 0;
        public Dictionary<string, object> OutBoxBag { get; set; } = new Dictionary<string, object>();

        public Publication Publication { get; set; }

        public Activity Span { get; set; }

        private MQTTMessagePublisher _mqttMessagePublisher;

        /// <summary>
        /// Initializes a new instance of the <see cref="MQTTMessageProducer" /> class.
        /// </summary>
        /// <param name="mqttMessagePublisher">The publisher used to send messages</param>
        public MQTTMessageProducer(MQTTMessagePublisher mqttMessagePublisher)
        {
            _mqttMessagePublisher = mqttMessagePublisher;
        }

        public void Dispose()
        {
            _mqttMessagePublisher = null;
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Send(Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _mqttMessagePublisher.PublishMessage(message);
        }

        /// <summary>
        /// Sends the specified message asynchronously.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public async Task SendAsync(Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            await _mqttMessagePublisher.PublishMessageAsync(message);
        }


        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">Delay to delivery of the message.</param>
        public void SendWithDelay(Message message, TimeSpan? delay = null)
        {
            delay ??= TimeSpan.Zero;
            
            //TODO: This is a blocking call, we should replace with a Time call
            Task.Delay(delay.Value);
            Send(message);
        }
    }
}
