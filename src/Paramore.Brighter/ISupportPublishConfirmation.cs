using System;

namespace Paramore.Brighter
{
    /// <summary>
    /// If this producer support a callback for confirmation of message send then it will return after calling send, but before the message is successfully
    /// persisted to the broker. It will then callback to confirm that the message has persisted to the broker, or not.
    /// Implementing producers should raise the OnMessagePublished event (using a threadpool thread) when the broker returns results
    /// The CommandProcessor will only mark a message as dispatched from the Outbox when this confirmation is received.
    /// </summary>
    public interface ISupportPublishConfirmation
    {
        /// <summary>
        /// Fired when a confirmation is received that a message has been published
        /// bool => was the message published
        /// Guid => what was the id of the published message
        /// </summary>
        event Action<bool, string> OnMessagePublished;
   }
}
