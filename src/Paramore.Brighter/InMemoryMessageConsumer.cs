﻿#region Licence
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

namespace Paramore.Brighter;

/// <summary>
/// An in memory consumer that reads from the Internal Bus. Mostly used for testing. Can be used with <see cref="Paramore.Brighter.InMemoryProducer"/>
/// and <see cref="Paramore.Brighter.InternalBus"/> to provide an in-memory message bus for a modular monolith.
/// If you set an ackTimeout then the consumer will requeue the message if it is not acknowledged
/// within the timeout. This is controlled by a background thread that checks the messages in the locked list
/// and requeues them if they have been locked for longer than the timeout.
/// </summary>
public class InMemoryMessageConsumer : IAmAMessageConsumer
{
    private readonly ConcurrentDictionary<string, LockedMessage> _lockedMessages = new();
    private readonly RoutingKey _topic;
    private readonly InternalBus _bus;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _ackTimeout;
    private readonly ITimer _lockTimer;
    private ITimer _requeueTimer;

    /// <summary>
    /// An in memory consumer that reads from the Internal Bus. Mostly used for testing. Can be used with <see cref="Paramore.Brighter.InMemoryProducer"/>
    /// and <see cref="Paramore.Brighter.InternalBus"/> to provide an in-memory message bus for a modular monolith.
    /// If you set an <paramref name="ackTimeout"/> then the consumer will requeue the message if it is not acknowledged
    /// within the timeout. This is controlled by a background thread that checks the messages in the locked list
    /// and requeues them if they have been locked for longer than the timeout.
    /// </summary>
    /// <param name="topic">The <see cref="Paramore.Brighter.RoutingKey"/> that we want to consume from</param>
    /// <param name="bus">The <see cref="Paramore.Brighter.InternalBus"/> that we want to read the messages from</param>
    /// <param name="timeProvider">Allows us to use a timer that can be controlled from tests</param>
    /// <param name="ackTimeout">The period before we requeue an unacknowledged message; defaults to -1ms or infinite</param>
    public InMemoryMessageConsumer(RoutingKey topic, InternalBus bus, TimeProvider timeProvider, TimeSpan? ackTimeout = null)
    {
        _topic = topic;
        _bus = bus;
        _timeProvider = timeProvider;
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
    /// Acknowledges the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Acknowledge(Message message)
    {
        _lockedMessages.TryRemove(message.Id, out _);
    }

    /// <summary>
    /// Rejects the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Reject(Message message)
    {
        _lockedMessages.TryRemove(message.Id, out _);
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
    /// Requeues the specified message.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="timeOut">Time span to delay delivery of the message. Defaults to 0ms</param>
    /// <returns>True if the message should be acked, false otherwise</returns>
    public bool Requeue(Message message, TimeSpan? timeOut = null)
    {
        timeOut ??= TimeSpan.Zero;
        
        if (timeOut <= TimeSpan.Zero)
            return RequeueNoDelay(message);

        //we don't want to block, so we use a timer to invoke the requeue after a delay
        _requeueTimer = _timeProvider.CreateTimer(
            msg => RequeueNoDelay((Message)msg), 
            message, 
            timeOut.Value, 
            TimeSpan.Zero
        );

        return true;
    }

    public void Dispose()
    {
        _lockTimer.Dispose();
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
    
    private bool RequeueNoDelay(Message message)
    {
        try
        {
            _lockedMessages.TryRemove(message.Id, out _); //--allow requeue even if not from locked msg in bus
            _bus.Enqueue(message);
            return true;

        }
        finally
        {
            _requeueTimer?.Dispose();
        }
    }

    private record LockedMessage(Message Message, DateTimeOffset LockedAt);
}
