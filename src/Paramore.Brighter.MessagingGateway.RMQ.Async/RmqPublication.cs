using System;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async
{
    public class RmqPublication : Publication
    {
        /// <summary>
        /// How long should we wait on shutdown for the broker to finish confirming delivery of messages
        /// If we shut down without confirmation then messages will not be marked as sent in the Outbox
        /// Any sweeper will then resend.
        /// </summary>
        public int WaitForConfirmsTimeOutInMilliseconds { get; set; } = 500;
    }
    
    /// <summary>
    /// Represents a publication for RabbitMQ (RMQ), associating a specific message type with the publication.
    /// This allows for strongly-typed publication to a RabbitMQ exchange or queue.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the request (message) that this publication handles.
    /// This type must be a class and implement the <see cref="IRequest"/> interface.
    /// </typeparam>
    public class RmqPublication<T> : RmqPublication 
       where T: class, IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RmqPublication{T}"/> class.
        /// </summary>
        public RmqPublication()
        {
            RequestType = typeof(T);
        }
    }
}
