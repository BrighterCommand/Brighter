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
    private ConcurrentDictionary<RoutingKey, BlockingCollection<Message>> _messages = new();

    /// <summary>
    /// Enqueue a message to tbe bus
    /// </summary>
    /// <param name="message">The message to enqueue</param>
    /// <param name="millisecondsTimeout">How long to wait for an item; -1 is forever; default is -1</param>
    public void Enqueue(Message message, int millisecondsTimeout = -1)
    {
        ValidateMillisecondsTimeout(millisecondsTimeout);
        
        var topic = new RoutingKey(message.Header.Topic);
        
        if (!_messages.ContainsKey(topic))
        {
            var blockingCollection = boundedCapacity > 0 ? 
                new BlockingCollection<Message>(boundedCapacity) : new BlockingCollection<Message>();
            
            if (!_messages.TryAdd(topic, blockingCollection) && !_messages.ContainsKey(topic))
                throw new InvalidOperationException("Failed to add topic to the bus");
        }
        
        if (!_messages[topic].TryAdd(message, millisecondsTimeout, CancellationToken.None))
            throw new InvalidOperationException("Failed to add message to the bus");
    }

    /// <summary>
    /// Dequeue a message from the bus
    /// </summary>
    /// <param name="topic">The topic to pull the message from</param>
    /// <param name="millisecondsTimeout">How long to wait for an item; -1 is forever; default is -1</param>
    /// <returns></returns>
    public Message Dequeue(RoutingKey topic, int millisecondsTimeout = -1)
    {
        ValidateMillisecondsTimeout(millisecondsTimeout);
        
        var found = _messages.TryGetValue(topic, out var messages);
        
        if (!found || !messages.Any())
            return MessageFactory.CreateEmptyMessage();

        if (!messages.TryTake(out Message message, millisecondsTimeout, CancellationToken.None))
            message = MessageFactory.CreateEmptyMessage();
        
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
        
        return messages != null ? messages.ToArray() : Array.Empty<Message>();
    }   
    
    private static void ValidateMillisecondsTimeout(int millisecondsTimeout)
    {
        if (millisecondsTimeout < 0 && millisecondsTimeout != -1)
            throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout), millisecondsTimeout, string.Format(CultureInfo.CurrentCulture, "Timeout must be greater than or equal to -1, was {0}", millisecondsTimeout));
    }
}
