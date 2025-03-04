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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Paramore.Brighter;

/// <summary>
/// Mainly intended for tests provides an in-memory implementation of a message bus
/// It can be passed to InMemoryProducer and InMemoryConsumer to provide an in-memory message bus
/// </summary>
/// <param name="boundedCapacity">The maximum number of messages that can be enqueued; -1 is unbounded; default is -1</param>
public class InternalBus(int boundedCapacity = -1) : IAmABus
{
    private readonly ConcurrentDictionary<RoutingKey, BlockingCollection<Message>> _messages = new();

    /// <summary>
    /// Enqueue a message to tbe bus
    /// </summary>
    /// <param name="message">The message to enqueue</param>
    /// <param name="timeout">How long to wait for an item; -1 or null is forever; default is null</param>
    public void Enqueue(Message message, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromMilliseconds(-1);
        
        ValidateMillisecondsTimeout(timeout.Value);
        
        var topic = new RoutingKey(message.Header.Topic);
        
        if (!_messages.TryGetValue(topic, out var blockingCollection))
        {
            blockingCollection = boundedCapacity > 0 ? 
                new BlockingCollection<Message>(boundedCapacity) : new BlockingCollection<Message>();
            
            if (!_messages.TryAdd(topic, blockingCollection) && !_messages.ContainsKey(topic))
                throw new InvalidOperationException("Failed to add topic to the bus");
        }
        
        if (!blockingCollection.TryAdd(message, Convert.ToInt32(timeout.Value.TotalMilliseconds), CancellationToken.None))
            throw new InvalidOperationException("Failed to add message to the bus");
    }

    /// <summary>
    /// Dequeue a message from the bus
    /// </summary>
    /// <param name="topic">The topic to pull the message from</param>
    /// <param name="timeout">How long to wait for an item; -1ms or null is forever; default is -1ms</param>
    /// <returns></returns>
    public Message Dequeue(RoutingKey topic, TimeSpan? timeout = null)
    {
        timeout ??=TimeSpan.FromMilliseconds(-1);
        
        ValidateMillisecondsTimeout(timeout.Value);
        
        var found = _messages.TryGetValue(topic, out var messages);
        
        if (!found || messages is null || !messages.Any())
            return MessageFactory.CreateEmptyMessage(topic);

        if (!messages.TryTake(out Message? message, Convert.ToInt32(timeout.Value.TotalMilliseconds), CancellationToken.None))
            message = MessageFactory.CreateEmptyMessage(topic);
        
        return message;
    }

    /// <summary>
    /// For a given topic, list all the messages
    /// </summary>
    /// <param name="topic">The topic we want messages for</param>
    /// <returns></returns>
    public IEnumerable<Message> Stream(RoutingKey topic)
    {
        _messages.TryGetValue(topic, out var messages);
        
        return messages != null ? messages.ToArray() : [];
    }   
    
    private static void ValidateMillisecondsTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero && timeout != TimeSpan.FromMilliseconds(-1))
            throw new ArgumentOutOfRangeException(nameof(timeout), Convert.ToInt32(timeout.TotalMilliseconds), string.Format(CultureInfo.CurrentCulture, "Timeout must be greater than or equal to -1ms, was {0}", Convert.ToInt32(timeout.TotalMilliseconds)));
    }
}
