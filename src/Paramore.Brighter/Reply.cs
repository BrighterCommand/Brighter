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
    public class Reply : Command, IResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier used to correlate this reply with a specific request.
        /// </summary>
        /// <remarks>
        /// This property is typically used to track and match replies to their originating requests,
        /// ensuring proper communication flow in distributed systems.
        /// </remarks>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// The channel that we should reply to the sender on.
        /// </summary>
        public ReplyAddress SendersAddress { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Reply"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the reply.</param>
        /// <param name="sendersAddress">The <see cref="ReplyAddress"/> representing the sender's address for the reply.</param>
        /// <param name="correlationId">The correlation identifier used to associate the reply with a specific request.</param>
        public Reply(string id, ReplyAddress sendersAddress, Guid correlationId)
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
        /// <param name="correlationId">The correlation identifier used to associate the reply with a specific request.</param>
        public Reply(Guid id, ReplyAddress sendersAddress, Guid correlationId)
            : this(id.ToString(), sendersAddress, correlationId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Reply"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the reply.</param>
        /// <param name="sendersAddress">The <see cref="ReplyAddress"/> representing the sender's address for the reply.</param>
        public Reply(string id, ReplyAddress sendersAddress)
            : this(id, sendersAddress, SenderCorrelationIdOrDefault(sendersAddress))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Reply"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the reply.</param>
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
            : this(Guid.NewGuid(), sendersAddress)
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
        public static Guid SenderCorrelationIdOrDefault(ReplyAddress sendersAddress)
        {
            return sendersAddress is not null && Guid.TryParse(sendersAddress.CorrelationId, out Guid correlationId)
                ? correlationId
                : Guid.NewGuid();
        }
    }
}
