#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
    /// Class ReplyAddress.
    /// The address to reply to when doing request-reply and not publish-subscribe messaging.
    /// </summary>
    /// <remarks>
    /// Used in request-reply patterns to specify where a response should be sent.
    /// Contains both the routing information (topic) and correlation data to match responses to requests.
    /// </remarks>
    public class ReplyAddress
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReplyAddress"/> class.
        /// </summary>
        /// <param name="topic">The <see cref="string"/> topic name where replies should be sent.</param>
        /// <param name="correlationId">The <see cref="string"/> correlation identifier to match requests and responses.</param>
        public ReplyAddress(string topic, Id correlationId)
        {
            Topic = new RoutingKey(topic);
            CorrelationId = correlationId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplyAddress"/> class.
        /// </summary>
        /// <param name="topic">The <see cref="RoutingKey"/> specifying where replies should be sent.</param>
        /// <param name="correlationId">The <see cref="string"/> correlation identifier to match requests and responses.</param>
        public ReplyAddress(RoutingKey topic, Id correlationId)
        {
            Topic = topic;
            CorrelationId = correlationId;
        }
        
        /// <summary>
        /// Gets or sets the topic.
        /// </summary>
        /// <value>The <see cref="RoutingKey"/> specifying the topic where replies should be sent.</value>
        public RoutingKey Topic { get; set; }

        /// <summary>
        /// Gets or sets the correlation identifier.
        /// </summary>
        /// <value>The <see cref="Id"/> correlation identifier used to match requests and responses.</value>
        public Id CorrelationId { get; set; }
    }
}
