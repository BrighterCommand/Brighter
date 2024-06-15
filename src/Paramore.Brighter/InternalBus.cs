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
using System.Linq;

namespace Paramore.Brighter;

/// <summary>
/// Mainly intended for tests provides an in-memory implementation of a message bus
/// It can be passed to InMemoryProducer and InMemoryConsumer to provide an in-memory message bus
/// </summary>
public class InternalBus : IAmABus
{
    private ConcurrentDictionary<RoutingKey, BlockingCollection<Message>> _messages = new();
    
    /// <summary>
    /// Enqueue a message to tbe bus
    /// </summary>
    /// <param name="message">The message to enqueue</param>
    public void Enqueue(Message message)
    {
        var topic = new RoutingKey(message.Header.Topic);
        
        if (!_messages.ContainsKey(topic))
        {
            _messages.TryAdd(topic, new BlockingCollection<Message>());
        }
        _messages[topic].Add(message);
    }

    /// <summary>
    /// Dequeue a message from the bus
    /// </summary>
    /// <param name="topic">The topic to pull the message from</param>
    /// <returns></returns>
    public Message Dequeue(RoutingKey topic)
    {
        var found = _messages.TryGetValue(topic, out var messages);
        
        if (!found || !messages.Any())
            return MessageFactory.CreateEmptyMessage();
            
        return messages?.Take();
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
}
