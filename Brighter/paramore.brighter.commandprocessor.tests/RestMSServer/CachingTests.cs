// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.restms.core.Ports.Cache;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Handlers;

namespace paramore.commandprocessor.tests.RestMSServer
{
    public class When_receiving_an_invalidate_cache_request
    {
        private const string RESOURCE_TO_INVALIDATE = "http://localhost:8080";
        private static IHandleRequests<InvalidateCacheCommand> s_cacheCleaner;
        private static InvalidateCacheCommand s_invalidateCacheCommand;
        private static IAmACache s_cache;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_cache = A.Fake<IAmACache>();

            s_cacheCleaner = new CacheCleaningHandler(s_cache, logger);
            s_invalidateCacheCommand = new InvalidateCacheCommand(new Uri(RESOURCE_TO_INVALIDATE));
        };

        private Because _of = () => s_cacheCleaner.Handle(s_invalidateCacheCommand);

        private It _should_clear_the_cache = () => A.CallTo(() => s_cache.InvalidateResource(new Uri(RESOURCE_TO_INVALIDATE))).MustHaveHappened();
    }
}
