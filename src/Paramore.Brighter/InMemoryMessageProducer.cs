#region Licence
/* The MIT License (MIT)
Copyright © 2015 Toby Henderson <hendersont@gmail.com>

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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    /// <summary>
    /// The in-memory producer is mainly intended for usage with tests. It allows you to send messages to a bus and
    /// then inspect the messages that have been sent.
    /// </summary>
    public sealed class InMemoryMessageProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync, IAmABulkMessageProducerAsync
    {
        private ITimer? _requeueTimer;
        private readonly IAmABus _bus;
        private readonly TimeProvider _timeProvider;
        private readonly InstrumentationOptions _instrumentationOptions;

        /// <summary>
        /// The in-memory producer is mainly intended for usage with tests. It allows you to send messages to a bus and
        /// then inspect the messages that have been sent.
        /// </summary>
        /// <param name="bus">An instance of <see cref="IAmABus"/> typically we use an <see cref="InternalBus"/></param>
        /// <param name="timeProvider">The <see cref="TimeProvider"/> we use to</param>
        /// <param name="publication">The <see cref="Publication"/> that we want to sent messages to via the publication; if null defaults to a Publication with a Topic of "Internal"</param>
        /// <param name="instrumentationOptions">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
        public InMemoryMessageProducer(IAmABus bus, TimeProvider? timeProvider = null, Publication? publication = null, InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
            _bus = bus;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _instrumentationOptions = instrumentationOptions;
            Publication = publication ?? new Publication { Topic = new RoutingKey("Internal") };
        }

        /// <summary>
        /// The publication that describes what the Producer is for
        /// </summary>
        public Publication Publication { get; set; }  

        /// <summary>
        /// Used for OTel tracing. We use property injection to set this, so that we can use the same tracer across all
        /// The producer is being used within the context of a CommandProcessor pipeline which will have initiated the trace
        /// or be using one from a containing framework like ASP.NET Core
        /// </summary>
        public Activity? Span { get; set; }

        /// <inheritdoc />
        public IAmAMessageScheduler? Scheduler { get; set; }

        /// <summary>
        /// What action should we take on confirmation that a message has been published to a broker
        /// </summary>
        public event Action<bool, Id>? OnMessagePublished;

        /// <summary>
        /// Dispose of the producer
        /// Clears the associated timer 
        /// </summary>
        public void Dispose()
        {
            if (_requeueTimer != null)_requeueTimer.Dispose();
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Dispose of the producer
        /// Clears the associated timer 
        /// </summary> 
        public async ValueTask DisposeAsync()
        {
            if (_requeueTimer != null) await _requeueTimer.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Send a message to a broker; in this case an <see cref="InternalBus"/>
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">Cancel the Send operation</param>
        /// <returns></returns>
        public Task SendAsync(Message message, CancellationToken cancellationToken = default)
        {
            BrighterTracer.WriteProducerEvent(Span, MessagingSystem.InternalBus, message, _instrumentationOptions);
            var tcs = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
            _bus.Enqueue(message);
            OnMessagePublished?.Invoke(true, message.Id);
            tcs.SetResult(message);
            return tcs.Task;
        }

        /// <summary>
        /// Send messages to a broker; in this case an <see cref="InternalBus"/> 
        /// </summary>
        /// <param name="messages">The list of messages to send</param>
        /// <param name="cancellationToken">A cancellation token to end the operation</param>
        /// <returns></returns>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<Id[]> SendAsync(
            IEnumerable<Message> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken
            )
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var msgs = messages as Message[] ?? messages.ToArray();
            foreach (var msg in msgs)
            {
                BrighterTracer.WriteProducerEvent(Span, MessagingSystem.InternalBus, msg, _instrumentationOptions);
                _bus.Enqueue(msg);
                OnMessagePublished?.Invoke(true, msg.Id);
                yield return [msg.Id];
            }
        }

        /// <summary>
        /// Send a message to a broker; in this case an <see cref="InternalBus"/>
        /// </summary>
        /// <param name="message">The message to send</param>
        public void Send(Message message)
        {
            BrighterTracer.WriteProducerEvent(Span, MessagingSystem.InternalBus, message, _instrumentationOptions);
            _bus.Enqueue(message);
            OnMessagePublished?.Invoke(true, message.Id);
        }

        /// <summary>
        /// Send a message to a broker; in this case an <see cref="InternalBus"/> with a delay
        /// The delay is simulated by the <see cref="TimeProvider"/>
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="delay">The delay of the send</param>
        public void SendWithDelay(Message message, TimeSpan? delay = null)
        {
            delay ??= TimeSpan.FromMilliseconds(0);

            //we don't want to block, so we use a timer to invoke the requeue after a delay
            _requeueTimer = _timeProvider.CreateTimer(
                msg => Send((Message)msg!),
                message,
                delay.Value,
                TimeSpan.Zero
            );
        }
  
        /// <summary>
        /// Send a message to a broker; in this case an <see cref="InternalBus"/> with a delay
        /// The delay is simulated by the <see cref="TimeProvider"/>
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="delay">The delay of the send</param>
        /// <param name="cancellationToken">A cancellation token for send operation</param>
        public Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        {
            delay ??= TimeSpan.FromMilliseconds(0);

            //we don't want to block, so we use a timer to invoke the requeue after a delay
            _requeueTimer = _timeProvider.CreateTimer(
                msg => SendAsync((Message)msg!, cancellationToken),
                message,
                delay.Value,
                TimeSpan.Zero
            );
            
            return Task.CompletedTask;
        }
    }
}
