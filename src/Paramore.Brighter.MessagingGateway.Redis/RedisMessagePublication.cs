using System;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessagePublication : Publication
    {
        //placeholder
    }

    /// <summary>
    /// Represents a publication for Redis messages, associating a specific message type with the publication.
    /// This allows for strongly-typed publication to a Redis channel or stream.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the request (message) that this publication handles.
    /// This type must be a class and implement the <see cref="IRequest"/> interface.
    /// </typeparam>
    public class RedisMessagePublication<T> : RedisMessagePublication
        where T: class, IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RedisMessagePublication{T}"/> class.
        /// </summary>
        public RedisMessagePublication()
        {
            RequestType = typeof(T);
        }
    }
}
