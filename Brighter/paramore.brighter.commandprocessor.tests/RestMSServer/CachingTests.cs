using System;
using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Ports.Cache;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Handlers;

namespace paramore.commandprocessor.tests.RestMSServer
{
    public class When_receiving_an_invalidate_cache_request
    {
        const string RESOURCE_TO_INVALIDATE = "http://localhost:8080";
        static IHandleRequests<InvalidateCacheCommand> cacheCleaner;
        static InvalidateCacheCommand invalidateCacheCommand;
        static IAmACache cache;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            cache = A.Fake<IAmACache>();

            cacheCleaner = new CacheCleaningHandler(cache, logger);
            invalidateCacheCommand = new InvalidateCacheCommand(new Uri(RESOURCE_TO_INVALIDATE));
        };

        Because of = () => cacheCleaner.Handle(invalidateCacheCommand);

        It should_clear_the_cache = () => A.CallTo(() => cache.InvalidateResource(new Uri(RESOURCE_TO_INVALIDATE))).MustHaveHappened();
    }
}
