using System;

namespace paramore.brighter.restms.core.Ports.Cache
{
    public interface IAmACache
    {
        void InvalidateResource(Uri resourceToInvalidate);
    }
}
