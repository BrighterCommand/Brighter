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
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// The in-memory producer is mainly intended for usage with tests. It allows you to send messages to a bus and
    /// then inspect the messages that have been sent.
    /// </summary>
    public sealed class InMemoryMessageProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync, IAmABulkMessageProducerAsync, ISupportPublishConfirmation, ISupportPublishConfirmationAsync
    {
        private readonly IAmABus _bus;
        private readonly InstrumentationOptions _instrumentationOptions;
        private readonly System.Threading.Channels.Channel<WorkItem> _channel =
            System.Threading.Channels.Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions { SingleReader = true });
        private readonly List<Task> _raiseTasks = new(); // drain worker is single-threaded; read only after worker completes
        private event Func<PublishConfirmationResult, Task>? _onMessagePublishedAsync;
        // volatile so a DisposeAsync on another thread sees the worker the CAS winner publishes (NFR-3)
        private volatile Task? _worker;
        private int _pumpStarted; // 0 = not started, 1 = started; CAS guard

        private readonly record struct WorkItem(Message Message, ActivityContext? Context);

        /// <summary>
        /// The in-memory producer is mainly intended for usage with tests. It allows you to send messages to a bus and
        /// then inspect the messages that have been sent.
        /// </summary>
        /// <param name="bus">An instance of <see cref="IAmABus"/> typically we use an <see cref="InternalBus"/></param>
        /// <param name="publication">The <see cref="Publication"/> that we want to sent messages to via the publication; if null defaults to a Publication with a Topic of "Internal"</param>
        /// <param name="instrumentationOptions">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
        public InMemoryMessageProducer(IAmABus bus, Publication? publication = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
            _bus = bus;
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
        /// When <see langword="false"/> (the default), <see cref="Send"/> and <see cref="SendAsync"/> write to the
        /// <see cref="InternalBus"/> and raise <see cref="OnMessagePublished"/> inline before returning — identical
        /// to today's behavior.  When <see langword="true"/>, sends are fire-and-forget: the work-item is enqueued
        /// onto an internal channel and the bus write + confirmation raise happen asynchronously on a single
        /// background worker.
        /// </summary>
        /// <remarks>
        /// This is an init-only property: set it once via an object initializer at construction time.  Changing it
        /// after the first send would leave the channel and worker in an inconsistent state.
        /// <para>
        /// This switch is an opt-in affordance for tests and local development (e.g. emulating a broker-backed
        /// confirmation producer in a modular monolith, or before moving to persistent storage); it is
        /// <see langword="false"/> by default so existing in-process behavior is preserved.  It toggles only this
        /// in-memory provider's confirm timing — it does <b>not</b> change the mediator's always-on failed-delivery
        /// observability and circuit-breaker behavior.
        /// </para>
        /// <para>
        /// <b>Deferred bus visibility:</b> when <see langword="true"/>, <see cref="Send"/>/<see cref="SendAsync"/>
        /// return <i>before</i> the message is written to the <see cref="InternalBus"/> (the worker writes it), so a
        /// caller that synchronously inspects the bus immediately after sending may not see the message yet.  Tests
        /// must await the confirmation (or poll the bus) rather than read it immediately.
        /// </para>
        /// </remarks>
        public bool UseAsyncPublishConfirmation { get; init; } = false;

        /// <summary>
        /// An optional predicate evaluated per message at send time.  When the predicate returns
        /// <see langword="true"/> the message is NOT written to the bus and a failure
        /// <see cref="PublishConfirmationResult"/> is raised instead — useful for injecting publish
        /// failures in tests.  <see langword="null"/> (the default) and a predicate returning
        /// <see langword="false"/> both result in a normal successful send.
        /// </summary>
        /// <remarks>
        /// This is an init-only property: set it once via an object initializer at construction time.  Like
        /// <see cref="UseAsyncPublishConfirmation"/>, it is a test/local-development affordance for exercising the
        /// failed-delivery path in-process; it does not change the mediator's failed-delivery behavior.
        /// </remarks>
        public Func<Message, bool>? PublishFailurePredicate { get; init; }

        /// <summary>
        /// What action should we take on confirmation that a message has been published to a broker
        /// </summary>
        public event Action<PublishConfirmationResult>? OnMessagePublished;

        event Func<PublishConfirmationResult, Task> ISupportPublishConfirmationAsync.OnMessagePublishedAsync
        {
            add => _onMessagePublishedAsync += value;
            remove => _onMessagePublishedAsync -= value;
        }

        /// <summary>
        /// Dispose of the producer. Blocks until any in-flight async pump has fully drained
        /// (channel completed, worker finished, all confirmation callbacks returned). The async
        /// drain is pumped on a dedicated single-threaded <see cref="BrighterAsyncContext"/> rather
        /// than blocking with <c>GetAwaiter().GetResult()</c>, so it does not deadlock when called
        /// from a thread that carries its own single-threaded synchronization context. Prefer
        /// <see cref="DisposeAsync"/> when the async confirmation pump is enabled.
        /// </summary>
        public void Dispose()
        {
            // The async pump is opt-in: when it never started there is nothing to drain, so keep
            // Dispose a true no-op rather than spinning up a single-threaded BrighterAsyncContext on
            // the default (sync-confirmation) path that the vast majority of callers use.
            if (Volatile.Read(ref _pumpStarted) == 0)
                return;
            BrighterAsyncContext.Run(async () => await DisposeAsync());
        }

        /// <summary>
        /// Dispose of the producer asynchronously. Two-stage drain: completes the channel
        /// writer so the pump loop exits, awaits the worker, then awaits every confirmation
        /// raise <see cref="Task"/> so callers can rely on all callbacks having fired before
        /// this returns.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            _channel.Writer.TryComplete();

            // Key the drain off _pumpStarted, not a snapshot of _worker: the CAS winner flips
            // _pumpStarted to 1 and only then publishes _worker, so a dispose racing the very first
            // enqueue could otherwise read a null _worker and skip the drain. If the pump was started
            // we spin (bounded — the publish is the next statement after the flip) until the volatile
            // _worker is visible, then await it so every enqueued confirmation has been pumped.
            if (Volatile.Read(ref _pumpStarted) == 1)
            {
                Task? worker;
                while ((worker = _worker) is null)
                    await Task.Yield();
                await worker;
            }

            // Safe to read _raiseTasks now: the single-threaded worker has completed, so all its
            // Add calls happened-before this point.
            await Task.WhenAll(_raiseTasks);
        }

        /// <summary>
        /// Send a message to a broker; in this case an <see cref="InternalBus"/>
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">Cancel the Send operation</param>
        public Task SendAsync(Message message, CancellationToken cancellationToken = default)
        {
            PublishMessage(message);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sends a batch of messages.
        /// </summary>
        /// <param name="batch">A batch of messages to send</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <exception cref="NotImplementedException"></exception>
        public Task SendAsync(IAmAMessageBatch batch, CancellationToken cancellationToken)
        {
            if (batch is not MessageBatch messageBatch)
                throw new NotImplementedException($"{nameof(SendAsync)} only supports ${typeof(MessageBatch)}");

            var messages = messageBatch.Content as Message[] ?? messageBatch.Content.ToArray();
            foreach (var message in messages)
                PublishMessage(message);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Creates message batches
        /// </summary>
        /// <param name="messages">A collection of messages to create batches for</param>
        /// <param name="cancellationToken">Allows cancellation of the ongoing operation</param>
        public ValueTask<IEnumerable<IAmAMessageBatch>> CreateBatchesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
            => new([new MessageBatch(messages)]);

        /// <summary>
        /// Send a message to a broker; in this case an <see cref="InternalBus"/>
        /// </summary>
        /// <param name="message">The message to send</param>
        public void Send(Message message) => PublishMessage(message);

        private void PublishMessage(Message message)
        {
            BrighterTracer.WriteProducerEvent(Span, MessagingSystem.InternalBus, message, _instrumentationOptions);
            if (UseAsyncPublishConfirmation)
                EnqueueDeferred(message);
            else
                Enqueue(message);
        }

        private void EnqueueDeferred(Message message)
        {
            // First-one-wins CAS on an int flag: only the thread that atomically transitions
            // _pumpStarted from 0→1 calls Task.Run. All concurrent first-enqueuers lose the
            // CAS and skip the start — preventing two readers against a SingleReader = true
            // channel (load-bearing for slice 7's two-stage dispose drain).
            // Capture before any await or channel handoff so the context is always from the caller's thread.
            var publishContext = Activity.Current?.Context;
            if (Interlocked.CompareExchange(ref _pumpStarted, 1, 0) == 0)
                _worker = Task.Run(DrainAsync);
            _channel.Writer.TryWrite(new WorkItem(message, publishContext));
        }

        private void Enqueue(Message message)
        {
            // The awaited event fires only on the deferred pump (UseAsyncPublishConfirmation true);
            // the synchronous path raises the fire-and-forget event only, preserving its non-blocking
            // send contract.
            if (PublishFailurePredicate?.Invoke(message) == true)
            {
                OnMessagePublished?.Invoke(new PublishConfirmationResult(false, message.Id, message.Header.Topic, null));
                return;
            }
            _bus.Enqueue(message);
            OnMessagePublished?.Invoke(new PublishConfirmationResult(true, message.Id, message.Header.Topic, null));
        }

        private async Task DrainAsync()
        {
            // External Action subscribers remain fire-and-forget; mediator confirmations use the awaited event.
            await foreach (var item in _channel.Reader.ReadAllAsync(CancellationToken.None))
            {
                if (PublishFailurePredicate?.Invoke(item.Message) == true)
                {
                    var failResult = new PublishConfirmationResult(false, item.Message.Id, item.Message.Header.Topic, item.Context);
                    _raiseTasks.Add(Task.Run(() => OnMessagePublished?.Invoke(failResult)));
                    await _onMessagePublishedAsync.InvokeAllAsync(failResult);
                    continue;
                }
                _bus.Enqueue(item.Message);
                var successResult = new PublishConfirmationResult(true, item.Message.Id, item.Message.Header.Topic, item.Context);
                _raiseTasks.Add(Task.Run(() => OnMessagePublished?.Invoke(successResult)));
                await _onMessagePublishedAsync.InvokeAllAsync(successResult);
            }
        }

        /// <summary>
        /// Send a message to a broker; in this case an <see cref="InternalBus"/> with a delay.
        /// When delay is zero or null, the message is sent immediately.
        /// When a scheduler is configured and delay is greater than zero, the scheduler is used.
        /// Otherwise, a <see cref="ConfigurationException"/> is thrown.
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="delay">The delay of the send</param>
        /// <exception cref="ConfigurationException">Thrown if scheduler not available</exception>
        public void SendWithDelay(Message message, TimeSpan? delay = null)
        {
            // Send immediately when no delay requested
            if (delay is null || delay <= TimeSpan.Zero)
            {
                Send(message);
                return;
            }

            // Use scheduler when configured and delay is greater than zero
            if (Scheduler is IAmAMessageSchedulerSync scheduler)
            {
                scheduler.Schedule(message, delay.Value);
                return;
            }
            
            throw new ConfigurationException($"Cannot requeue {message.Id} with delay; no scheduler is configured. Configure a scheduler via MessageSchedulerFactory in IAmProducersConfiguration."); 
 
        }
  
        /// <summary>
        /// Send a message to a broker; in this case an <see cref="InternalBus"/> with a delay.
        /// When delay is zero or null, the message is sent immediately.
        /// When an async scheduler is configured and delay is greater than zero, the scheduler is used.
        /// Otherwise, a <see cref="ConfigurationException"/> is thrown.
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="delay">The delay of the send</param>
        /// <param name="cancellationToken">A cancellation token for send operation</param>
        /// <exception cref="ConfigurationException">Thrown if scheduler not available</exception>
        public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        {
            // Send immediately when no delay requested
            if (delay is null || delay <= TimeSpan.Zero)
            {
                await SendAsync(message, cancellationToken);
                return;
            }

            // Use async scheduler when configured and delay is greater than zero
            if (Scheduler is IAmAMessageSchedulerAsync scheduler)
            {
                await scheduler.ScheduleAsync(message, delay.Value, cancellationToken);
                return;
            }

            throw new ConfigurationException($"Cannot requeue {message.Id} with delay; no scheduler is configured. Configure a scheduler via MessageSchedulerFactory in IAmProducersConfiguration."); 
        }
    }
}
