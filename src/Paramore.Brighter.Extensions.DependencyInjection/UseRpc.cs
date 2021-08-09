using System;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Options around use of RPC over messaging i.e. command and document response
    /// Requires blocking for a response on a queue identified by producer to consumer
    /// </summary>
    public class UseRpc : IUseRpc
    {
        public UseRpc(bool useRPC)
        {
            RPC = useRPC;
        }

        public bool RPC { get; set; }
    }
}
