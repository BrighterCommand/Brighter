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

namespace Paramore.Brighter;

public interface IAmAChannel : IDisposable
{
    /// <summary>
    /// Gets the name.
    /// </summary>
    /// <value>The name.</value>
    ChannelName Name { get; }

    /// <summary>
    /// The topic that this channel is for (how a broker routes to it)
    /// </summary>
    /// <value>The topic on the broker</value>
    RoutingKey RoutingKey { get; }

    /// <summary>
    /// Adds a message to the queue
    /// </summary>
    /// <param name="message"></param>
    void Enqueue(params Message[] message);

    /// <summary>
    /// Stops this instance.
    /// <param name="topic">The topic to post the MT_QUIT message too</param>
    /// </summary>
    void Stop(RoutingKey topic);

}
