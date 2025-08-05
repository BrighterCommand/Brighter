#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    /// <summary>
    /// The <see cref="MsSqlMessageProducer"/> class is responsible for producing messages to an MS SQL database.
    /// </summary>
    public partial class MsSqlMessageProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageProducer>();
        private readonly MsSqlMessageQueue<Message> _sqlQ;

        /// <summary>
        /// Gets the publication used to configure the producer.
        /// </summary>
        public Publication Publication { get; set; }

        /// <summary>
        /// Gets or sets the OTel span for writing producer events.
        /// </summary>
        public Activity? Span { get; set; }

        /// <inheritdoc />
        public IAmAMessageScheduler? Scheduler { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlMessageProducer"/> class.
        /// </summary>
        /// <param name="msSqlConfiguration">The MS SQL configuration.</param>
        /// <param name="connectonProvider">The connection provider.</param>
        /// <param name="publication">The publication configuration.</param>
        public MsSqlMessageProducer(
            RelationalDatabaseConfiguration msSqlConfiguration,
            IAmARelationalDbConnectionProvider connectonProvider,
            Publication? publication = null
        )
        {
            _sqlQ = new MsSqlMessageQueue<Message>(msSqlConfiguration, connectonProvider);
            Publication = publication ?? new Publication { MakeChannels = OnMissingChannel.Create };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlMessageProducer"/> class.
        /// </summary>
        /// <param name="msSqlConfiguration">The MS SQL configuration.</param>
        /// <param name="publication">The publication configuration.</param>
        public MsSqlMessageProducer(
            RelationalDatabaseConfiguration msSqlConfiguration,
            Publication? publication = null)
            : this(msSqlConfiguration, new MsSqlConnectionProvider(msSqlConfiguration), publication)
        {
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void Send(Message message)
        {
            SendWithDelay(message, TimeSpan.Zero);
        }

        /// <summary>
        /// Sends the specified message asynchronously.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
        {
            await SendWithDelayAsync(message, TimeSpan.Zero, cancellationToken);
        }

        /// <summary>
        /// Sends the specified message with a delay.
        /// </summary>
        /// <remarks>No delay support implemented.</remarks>
        /// <param name="message">The message to send.</param>
        /// <param name="delay">The delay to use.</param>
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
                  
                Log.NoSchedulerConfigured(s_logger);
            }
              
            var topic = message.Header.Topic;

            Log.SendMessage(s_logger, topic, message.Id);

            _sqlQ.Send(message, topic);
        }

        /// <summary>
        /// Sends the specified message with a delay asynchronously.
        /// </summary>
        /// <remarks>No delay support implemented.</remarks>
        /// <param name="message">The message to send.</param>
        /// <param name="delay">The delay to use.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
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
                
                Log.NoSchedulerConfigured(s_logger);
            }

            var topic = message.Header.Topic;

            Log.SendMessageAsync(s_logger, topic, message.Id);

            await _sqlQ.SendAsync(message, topic, TimeSpan.Zero, cancellationToken);
        }

        /// <summary>
        /// Disposes the message producer.
        /// </summary>
        public void Dispose() { }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(Task.CompletedTask);
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "MsSqlMessageProducer: no scheduler configured, message will be sent immediately")]
            public static partial void NoSchedulerConfigured(ILogger logger);

            [LoggerMessage(LogLevel.Debug, "MsSqlMessageProducer: send message with topic {Topic} and id {Id}")]
            public static partial void SendMessage(ILogger logger, string topic, string id);
            
            [LoggerMessage(LogLevel.Debug, "MsSqlMessageProducer: send async message with topic {Topic} and id {Id}")]
            public static partial void SendMessageAsync(ILogger logger, string topic, string id);
        }
    }
}

