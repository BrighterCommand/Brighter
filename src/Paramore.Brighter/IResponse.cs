using System;

namespace Paramore.Brighter
{
    /// <summary>
    /// A Response to a Request. We derive from IRequest because our message handling infrastructure receives requests
    /// even if in a Request-Response paradigm that is a response to another request.
    /// </summary>
    public interface IResponse : IRequest
    {
        /// <summary>
        /// Allow us to correlate request and response
        /// </summary>
        Guid CorrelationId { get; }
  }
}
