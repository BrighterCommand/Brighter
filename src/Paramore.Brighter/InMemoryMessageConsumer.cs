#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter;

/// <summary>
/// An in memory consumer that reads from the Internal Bus. Mostly used for testing. Can be used with <see cref="InMemoryMessageProducer"/>
/// and <see cref="Paramore.Brighter.InternalBus"/> to provide an in-memory message bus for a modular monolith.
/// If you set an ackTimeout then the consumer will requeue the message if it is not acknowledged
/// within the timeout. This is controlled by a background thread that checks the messages in the locked list
/// and re-queues them if they have been locked for longer than the timeout.
/// </summary>
public sealed class InMemoryMessageConsumer : IAmAMessageConsumerSync, IAmAMessageConsumerAsync
{
    private readonly ConcurrentDictionary<string, LockedMessage> _lockedMessages = new();
    private readonly RoutingKey _topic;
    private readonly RoutingKey? _deadLetterTopic;
    private readonly RoutingKey? _invalidMessageTopic;
    private readonly InternalBus _bus;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _ackTimeout;
    private readonly ITimer _lockTimer;
    private readonly IAmAMessageScheduler? _scheduler;
    private InMemoryMessageProducer? _requeueProducer;
    private volatile bool _requeueProducerInitialized;
    private object? _requeueProducerLock;

    /// <summary>
    /// An in memory consumer that reads from the Internal Bus. Mostly used for testing. Can be used with <see cref="InMemoryMessageProducer"/>
    /// and <see cref="Paramore.Brighter.InternalBus"/> to provide an in-memory message bus for a modular monolith.
    /// If you set an <paramref name="ackTimeout"/> then the consumer will requeue the message if it is not acknowledged
    /// within the timeout. This is controlled by a background thread that checks the messages in the locked list
    /// and requeues them if they have been locked for longer than the timeout.
    /// </summary>
    /// <remarks>
    /// If an <see cref="invalidMessageTopic"/> is not provided but a <see cref="deadLetterTopic"/> is, then we will treat
    /// the <see cref="deadLetterTopic"/> as the topic for invalid messages
    /// </remarks>
    /// <param name="topic">The <see cref="Paramore.Brighter.RoutingKey"/> that we want to consume from</param>
    /// <param name="bus">The <see cref="Paramore.Brighter.InternalBus"/> that we want to read the messages from</param>
    /// <param name="timeProvider">Allows us to use a timer that can be controlled from tests</param>
    /// <param name="deadLetterTopic">If a dead letter channel is required, then provide a topic to use</param>
    /// <param name="invalidMessageTopic">If an invalid message channel is required, then provide a topic to use</param>
    /// <param name="ackTimeout">The period before we requeue an unacknowledged message; defaults to -1ms or infinite</param>
    /// <param name="scheduler">Optional scheduler for delayed requeue operations</param>
    public InMemoryMessageConsumer(RoutingKey topic,
        InternalBus bus,
        TimeProvider timeProvider,
        RoutingKey? deadLetterTopic = null,
        RoutingKey? invalidMessageTopic = null,
        TimeSpan? ackTimeout = null,
        IAmAMessageScheduler? scheduler = null)
    {
        _topic = topic;
        _deadLetterTopic = deadLetterTopic;
        _invalidMessageTopic = invalidMessageTopic;
        _bus = bus;
        _timeProvider = timeProvider;
        _scheduler = scheduler;
        ackTimeout ??= TimeSpan.FromMilliseconds(-1);
        _ackTimeout = ackTimeout.Value;

        _lockTimer = _timeProvider.CreateTimer(
            _ => CheckLockedMessages(),
            null,
            _ackTimeout,
            _ackTimeout
        );

    }
    
    /// <summary>
    /// Disposes of the consumer, will remove timers, producers, etc.
    /// </summary>
    ~InMemoryMessageConsumer()
    {
        _lockTimer.Dispose();
    }


    /// <summary>
    /// Acknowledges the specified message.
    /// </summary>
    /// <remarks>
    /// When a message is acknowledged, another consumer should not process it
    /// </remarks>
    /// <param name="message">The<see cref="Message"/> to acknowledged</param>
    public void Acknowledge(Message message)
    {
        _lockedMessages.TryRemove(message.Id, out _);
    }

    /// <summary>
    /// Acknowledges the specified message.
    /// We use Task.Run here to emulate async
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancel the acknowledgement</param>
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Acknowledge(message), cancellationToken);
    }
  
    /// <summary>
    /// Nacks the specified message, removing it from the locked messages and re-enqueuing it to the bus
    /// so it is immediately available for redelivery.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> to nack</param>
    public void Nack(Message message)
    {
        _lockedMessages.TryRemove(message.Id, out _);
        _bus.Enqueue(message);
    }

    /// <summary>
    /// Nacks the specified message, removing it from the locked messages and re-enqueuing it to the bus
    /// so it is immediately available for redelivery.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> to nack</param>
    /// <param name="cancellationToken">Cancel the nack operation</param>
    public async Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Nack(message), cancellationToken);
    }

    /// <summary>
    /// Purges the specified queue name.
    /// </summary>
    public void Purge()
    {
        Message message;
        do {
            message = _bus.Dequeue(_topic);
        } while (message.Header.MessageType != MessageType.MT_NONE);
    }
    
    /// <summary>
    /// Purges the specified queue name.
    /// We use Task.Run here to emulate async
    /// </summary>
    /// <param name="cancellationToken">Cancel the purge</param>
    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Purge(), cancellationToken);
    }

    /// <summary>
    /// Receives the specified queue name.
    /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge the processing of those messages or requeue them.
    /// Used by a <see cref="Paramore.Brighter.Channel"/> to provide access to a third-party message queue.
    /// </summary>
    /// <param name="timeOut">The <see cref="TimeSpan"/> timeout</param>
    /// <returns>An array of Messages from middleware</returns>
    public Message[] Receive(TimeSpan? timeOut = null)
    {
        timeOut ??= TimeSpan.FromSeconds(1);
        
        var messages = new[] {_bus.Dequeue(_topic, timeOut)};
        foreach (var message in messages)
        {
            //don't lock empty messages
            if (message.Header.MessageType == MessageType.MT_NONE)
                continue;
            _lockedMessages.TryAdd(message.Id, new LockedMessage(message, _timeProvider.GetUtcNow()));
        }

        return messages;
    }

    /// <summary>
    /// Receives the specified queue name.
    /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge the processing of those messages or requeue them.
    /// Used by a <see cref="Paramore.Brighter.Channel"/> to provide access to a third-party message queue.
    /// We use Task.Run here to emulate async 
    /// </summary>
    /// <param name="timeOut">The <see cref="TimeSpan"/> timeout</param>
    /// <param name="cancellationToken">Cancel in the receive operation</param>
    /// <returns>An array of Messages from middleware</returns>
    public Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Receive(timeOut), cancellationToken);
    }
    
      /// <summary>
    /// Rejects the specified message.
    /// </summary>
    /// When a message is rejected, another consumer should not process it. If there is a dead letter, or invalid
    /// message channel, the message should be forwarded to it
    /// <param name="message">The <see cref="Message"/> to reject</param>
    /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
    /// <returns>True if the message has been removed from the channel, false otherwise</returns>
    public bool Reject(Message message, MessageRejectionReason? reason = null)
    {
        _lockedMessages.TryRemove(message.Id, out _);

        if (reason is { RejectionReason: RejectionReason.DeliveryError })
        {
            if ( _deadLetterTopic is null) return true;

            message.Header.Topic = _deadLetterTopic;
        }
        else if (reason is { RejectionReason: RejectionReason.Unacceptable })
        {
            if (_invalidMessageTopic is not null) 
                message.Header.Topic = _invalidMessageTopic;
            else if (_deadLetterTopic is not null)
                message.Header.Topic = _deadLetterTopic;
            else
                return true;
        }
        else if (reason is null)
        {
            if ( _deadLetterTopic is null) return true;

            message.Header.Topic = _deadLetterTopic;
        }    

        _bus.Enqueue(message);

        return true;
    }

    /// <summary>
    /// Rejects the specified message.
    /// </summary>
    /// When a message is rejected, another consumer should not process it. If there is a dead letter, or invalid
    /// message channel, the message should be forwarded to it
    /// <param name="message">The <see cref="Message"/> to reject</param>
    /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
    /// <param name="cancellationToken">Cancels the rejection</param>
    /// <returns>True if the message has been removed from the channel, false otherwise</returns>
    public async Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Reject(message, reason), cancellationToken);
    }

    /// <summary>
    /// Requeues the specified message.
    /// When a scheduler is configured and timeout is greater than zero, delegates to producer's SendWithDelay
    /// which uses the scheduler for delayed delivery.
    /// </summary>
    /// <param name="message">The message to requeue</param>
    /// <param name="timeOut">Time span to delay delivery of the message. Defaults to 0ms</param>
    /// <returns>True if the message should be acked, false otherwise</returns>
    /// <exception cref="ConfigurationException">Thrown when a delay is requested but no scheduler is configured.</exception>
    /// <remarks>The requeue method will use the topic of the first message that it receives to create a producer, and use that to requeue</remarks>
    public bool Requeue(Message message, TimeSpan? timeOut = null)
    {
        timeOut ??= TimeSpan.Zero;

        if (timeOut <= TimeSpan.Zero)
            return RequeueNoDelay(message);

        // Use producer delegation when scheduler is configured
        if (_scheduler != null)
        {
            try
            {
                _lockedMessages.TryRemove(message.Id, out _);
                EnsureProducer(message.Header.Topic);
                _requeueProducer!.SendWithDelay(message, timeOut);
                return true;
            }
            catch
            {
                _lockedMessages.TryAdd(message.Id, new LockedMessage(message, _timeProvider.GetUtcNow()));
                throw;
            }
        }

        throw new ConfigurationException($"Cannot requeue {message.Id} with delay; no scheduler is configured. Configure a scheduler via MessageSchedulerFactory in IAmProducersConfiguration.");

    }

    /// <summary>
    /// Requeues the specified message.
    /// When a scheduler is configured and timeout is greater than zero, delegates to producer's SendWithDelayAsync
    /// which uses the async scheduler for delayed delivery.
    /// </summary>
    /// <param name="message">The message to requeue</param>
    /// <param name="timeOut">Time span to delay delivery of the message. Defaults to 0ms</param>
    /// <param name="cancellationToken">Allows the asynchronous operation to be cancelled</param>
    /// <returns>True if the message should be acked, false otherwise</returns>
    /// <exception cref="ConfigurationException">Thrown when a delay is requested but no scheduler is configured.</exception>
    /// <remarks>The requeue method will use the topic of the first message that it receives to create a producer, and use that to requeue</remarks>
    public async Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        timeOut ??= TimeSpan.Zero;

        if (timeOut <= TimeSpan.Zero)
            return RequeueNoDelay(message);

        // Use producer delegation when scheduler is configured
        if (_scheduler != null)
        {
            try
            {
                _lockedMessages.TryRemove(message.Id, out _);
                EnsureProducer(message.Header.Topic);
                await _requeueProducer!.SendWithDelayAsync(message, timeOut, cancellationToken);
                return true;
            }
            catch
            {
                _lockedMessages.TryAdd(message.Id, new LockedMessage(message, _timeProvider.GetUtcNow()));
                throw;
            }
        }

        throw new ConfigurationException($"Cannot requeue {message.Id} with delay; no scheduler is configured. Configure a scheduler via MessageSchedulerFactory in IAmProducersConfiguration."); 
    }

    /// <inheritdoc cref="IDisposable"/>
    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc cref="IAsyncDisposable"/> 
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this); 
    }
    

    private void CheckLockedMessages()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var lockedMessage in _lockedMessages)
        {
            if (now - lockedMessage.Value.LockedAt > _ackTimeout)
            {
                RequeueNoDelay(lockedMessage.Value.Message);
            }
        }
    }

    private void DisposeCore()
    {
        _lockTimer.Dispose();
        _requeueProducer?.Dispose();
    }

    private async ValueTask DisposeAsyncCore()
    {
        await _lockTimer.DisposeAsync().ConfigureAwait(false);
        if (_requeueProducer != null) await _requeueProducer.DisposeAsync().ConfigureAwait(false);
    }

    private void EnsureProducer(RoutingKey topic)
    {
#pragma warning disable CS0420 // LazyInitializer handles the memory barrier for the volatile field
        LazyInitializer.EnsureInitialized(ref _requeueProducer, ref _requeueProducerInitialized,
            ref _requeueProducerLock, () => new InMemoryMessageProducer(_bus, new Publication { Topic = topic })
            {
                Scheduler = _scheduler
            });
#pragma warning restore CS0420
    }
    
    private bool RequeueNoDelay(Message message)
    {
        _lockedMessages.TryRemove(message.Id, out _); //--allow requeue even if not from locked msg in bus
        _bus.Enqueue(message);
        return true;
    }

    private sealed record LockedMessage(Message Message, DateTimeOffset LockedAt);

}
