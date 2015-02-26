// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using CacheCow.Server;
using paramore.brighter.restms.core.Ports.Cache;

namespace paramore.brighter.restms.server.Adapters.Cache
{
    internal class CacheHandler : IAmACache
    {
        private readonly ICachingHandler _cachingHandler;

        public CacheHandler(ICachingHandler cachingHandler)
        {
            _cachingHandler = cachingHandler;
        }

        public void InvalidateResource(Uri resourceToInvalidate)
        {
            _cachingHandler.InvalidateResource(new HttpRequestMessage(HttpMethod.Get, resourceToInvalidate));
        }
    }
}
