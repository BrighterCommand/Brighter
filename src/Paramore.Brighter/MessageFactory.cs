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

namespace Paramore.Brighter
{
    /// <summary>
    /// Used to create off-the-shelf messages: empty, and quit, used to control processing.
    /// </summary>
    public static class MessageFactory
    {
        /// <summary>
        /// Creates the quit message.
        /// </summary>
        /// <returns>Message.</returns>
        public static Message CreateQuitMessage(RoutingKey routingKey)
        {
            return new Message(new MessageHeader(Id.Empty, routingKey, MessageType.MT_QUIT), new MessageBody(string.Empty));
        }
        
        /// <summary>
        /// Creates an empty message; this sets the message type to MT_NONE
        /// </summary>
        /// <returns>An Empty message</returns>
        public static Message CreateEmptyMessage(RoutingKey routingKey)
        {
            return new Message(new MessageHeader(Id.Empty, routingKey, MessageType.MT_NONE), new MessageBody(string.Empty));
        }
    }
}
