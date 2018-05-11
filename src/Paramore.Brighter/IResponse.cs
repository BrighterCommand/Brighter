using System;

namespace Paramore.Brighter
{
    public interface IResponse
    {
        /// <summary>
        /// Allow us to correlate request and response
        /// </summary>
        Guid CorrelationId { get; }
        
        /// <summary>
        /// The address of the queue to reply to - usually private to the sender
        /// </summary>
         ReplyAddress SendersAddress { get; }
    }
}
