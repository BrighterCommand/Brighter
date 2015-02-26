#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

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
