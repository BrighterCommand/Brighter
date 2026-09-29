#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class HandlerLifetimeScopePartialReleaseTests
    {
        [Test]
        public async System.Threading.Tasks.Task When_a_handler_release_throws_the_scope_should_still_release_the_rest()
        {
            // Arrange — three tracked handlers, a factory whose Release throws for the first, and a
            // recording IAmAScope handle supplied by that factory's CreatePipelineScope()
            using var context = TestCorrelator.CreateContext();

            var recorder = new ReleaseOrderRecorder();
            var scopeHandle = new RecordingPipelineScope(recorder);
            var factory = new RecordingReleaseHandlerFactory(recorder, scopeHandle);

            var first = new RecordingHandler("First");
            var second = new RecordingHandler("Second");
            var third = new RecordingHandler("Third");
            factory.ThrowFor(first);

            var lifetimeScope = new HandlerLifetimeScope(factory, scopeHandle);
            lifetimeScope.Add(first);
            lifetimeScope.Add(second);
            lifetimeScope.Add(third);

            // Act — Dispose() must return normally even though the first handler's Release throws
            lifetimeScope.Dispose();

            // Assert — the other two handlers were still released, both tracking lists were cleared,
            // and the handle was disposed, last, after every release
            await Assert.That(recorder.Events).Contains("Released:First");
            await Assert.That(recorder.Events).Contains("Released:Second");
            await Assert.That(recorder.Events).Contains("Released:Third");
            await Assert.That(scopeHandle.WasDisposed).IsTrue();
            await Assert.That(lifetimeScope.TrackedItemCount).IsEqualTo(0);
            await Assert.That(recorder.Events.Last()).IsEqualTo("ScopeDisposed");

            // exactly one Error record naming the failing release
            var errors = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Error)
                .ToList();
            await Assert.That(errors).HasSingleItem();
            await Assert.That(errors[0].RenderMessage()).Contains("First");
        }
    }
}
