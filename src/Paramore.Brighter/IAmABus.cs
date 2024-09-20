#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter;

/// <summary>
/// Defines an interface for a bus that can enqueue, dequeue and stream messages
/// Mainly used for an internal bus, but could be used for other implementations
/// </summary>
public interface IAmABus
{
    /// <summary>
    /// Enqueues a message onto the bus
    /// </summary>
    /// <param name="message">The <see cref="Message"/> to enqueue</param>
    /// <param name="timeout">The <see cref="TimeSpan"/> to use as a timeout; null or -1ms indicates forever; defaults to -1ms</param>
    void Enqueue(Message message, TimeSpan? timeout = null);
    Message Dequeue(RoutingKey topic, TimeSpan? timeout = null);
    IEnumerable<Message> Stream(RoutingKey topic);
}
