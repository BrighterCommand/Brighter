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
    /// Class Reply.
    /// Used for a Command that is a Reply in a Request-Reply exchange. Brighter supports publish-subscribe as its main approach to producers and consumers, but it is possible 
    /// to support request-reply semantics as well. The key is that the sender must include a <see cref="ReplyAddress"/> in the <see cref="Request"/> (the <see cref="IAmAMessageMapper"/>
    /// then populates that into the <see cref="MessageHeader"/> as the replyTo address). When we create a <see cref="Reply"/> then we set the <see cref="ReplyAddress"/> from the <see cref="Request"/>
    /// onto the <see cref="Reply"/> and the <see cref="IAmAMessageMapper"/> for the <see cref="Reply"/> sets this as the topic so that it is routed correctly.
    /// </summary>
    /// <remarks>
    /// This class implements both <see cref="Command"/> and <see cref="IResponse"/> to support
    /// sending replies back to the original requester in request-reply messaging patterns.
    /// </remarks>
    public class Reply : Command, IResponse
    {

        /// <summary>
        /// Gets the channel that we should reply to the sender on.
        /// </summary>
        /// <value>A <see cref="ReplyAddress"/> specifying where this reply should be sent.</value>
        public ReplyAddress SendersAddress { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Reply"/> class.
        /// </summary>
        /// <param name="id">The <see cref="string"/> unique identifier for the reply.</param>
        /// <param name="sendersAddress">The <see cref="ReplyAddress"/> representing the sender's address for the reply.</param>
        /// <param name="correlationId">The <see cref="Id"/> correlation identifier used to associate the reply with a specific request.</param>
        public Reply(string id, ReplyAddress sendersAddress, Id correlationId)
            : base(id)
        {
            SendersAddress = sendersAddress;
            CorrelationId = correlationId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Reply"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the reply, represented as a <see cref="Guid"/>.</param>
        /// <param name="sendersAddress">The <see cref="ReplyAddress"/> representing the sender's address for the reply.</param>
        /// <param name="correlationId">The <see cref="Id"/> correlation identifier used to associate the reply with a specific request.</param>
        public Reply(Guid id, ReplyAddress sendersAddress, Id correlationId)
            : this(id.ToString(), sendersAddress, correlationId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Reply"/> class.
        /// </summary>
        /// <param name="id">The <see cref="string"/> unique identifier for the reply.</param>
        /// <param name="sendersAddress">The <see cref="ReplyAddress"/> representing the sender's address for the reply.</param>
        public Reply(string id, ReplyAddress sendersAddress)
            : this(id, sendersAddress, SenderCorrelationIdOrDefault(sendersAddress))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Reply"/> class.
        /// </summary>
        /// <param name="id">The <see cref="Guid"/> unique identifier for the reply.</param>
        /// <param name="sendersAddress">The <see cref="ReplyAddress"/> representing the sender's address for the reply.</param>
        public Reply(Guid id, ReplyAddress sendersAddress)
            : this(id.ToString(), sendersAddress, SenderCorrelationIdOrDefault(sendersAddress))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Reply"/> class.
        /// </summary>
        /// <param name="sendersAddress">The <see cref="ReplyAddress"/> representing the sender's address for the reply.</param>
        public Reply(ReplyAddress sendersAddress)
            : this(Uuid.New(), sendersAddress)
        {
        }

        /// <summary>
        /// Retrieves the correlation ID from the provided <see cref="ReplyAddress"/> if it is valid; 
        /// otherwise, generates a new <see cref="Guid"/>.
        /// </summary>
        /// <param name="sendersAddress">The <see cref="ReplyAddress"/> containing the correlation ID.</param>
        /// <returns>
        /// A <see cref="Guid"/> representing the correlation ID from the <paramref name="sendersAddress"/> 
        /// if it is valid; otherwise, a newly generated <see cref="Guid"/>.
        /// </returns>
        /// <remarks>
        /// This method provides a safe way to extract correlation IDs from reply addresses,
        /// falling back to a new GUID if the provided correlation ID cannot be parsed.
        /// </remarks>
        public static Id SenderCorrelationIdOrDefault(ReplyAddress? sendersAddress)
        {
            return sendersAddress is not null ? sendersAddress.CorrelationId: Id.Random();
        }
    }
}
