using System.Collections.Generic;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Flags support is required for RPC over messaging
    /// For RPC a command object needs to return a value on a private queue
    /// This approach blocks waiting for a response
    /// </summary>
    public interface IUseRpc
    {
        bool RPC { get; set; }
        IEnumerable<Subscription> ReplyQueueSubscriptions { get; set; }
    }
}
