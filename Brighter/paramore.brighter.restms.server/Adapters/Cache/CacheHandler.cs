using System;
using System.Net.Http;
using CacheCow.Server;
using paramore.brighter.restms.core.Ports.Cache;

namespace paramore.brighter.restms.server.Adapters.Cache
{
    internal class CacheHandler : IAmACache
    {
        readonly ICachingHandler cachingHandler;

        public CacheHandler(ICachingHandler cachingHandler)
        {
            this.cachingHandler = cachingHandler;
        }

        public void InvalidateResource(Uri resourceToInvalidate)
        {
            cachingHandler.InvalidateResource(new HttpRequestMessage(HttpMethod.Get, resourceToInvalidate));
        }

    }
}
