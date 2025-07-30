#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
    /// Class Request.
    /// Used for a Command that is a Reply in a Request-Reply exchange. Brighter supports publish-subscribe as its main approach to producers and consumers, but it is possible 
    /// to support request-reply semantics as well. The key is that the sender must include a <see cref="ReplyAddress"/> in the <see cref="Request"/> (the <see cref="IAmAMessageMapper"/>
    /// then populates that into the <see cref="MessageHeader"/> as the replyTo address). When we create a <see cref="Reply"/> then we set the <see cref="ReplyAddress"/> from the <see cref="Request"/>
    /// onto the <see cref="Reply"/> and the <see cref="IAmAMessageMapper"/> for the <see cref="Reply"/> sets this as the topic so that it is routed correctly.
    /// </summary>
    /// <remarks>
    /// This class implements both <see cref="Command"/> and <see cref="ICall"/> to support request-reply patterns
    /// where the sender expects a response to their command.
    /// </remarks>
    public class Request : Command, ICall
    {
        /// <summary>
        /// Gets the address of the queue to reply to - usually private to the sender.
        /// </summary>
        /// <value>A <see cref="ReplyAddress"/> specifying where the response should be sent.</value>
        public ReplyAddress ReplyAddress { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Request"/> class.
        /// </summary>
        /// <param name="replyAddress">The <see cref="ReplyAddress"/> where the response should be sent.</param>
        public Request(ReplyAddress replyAddress)
            : base(Id.Random())
        {
            ReplyAddress = replyAddress;
        }
    }
}
