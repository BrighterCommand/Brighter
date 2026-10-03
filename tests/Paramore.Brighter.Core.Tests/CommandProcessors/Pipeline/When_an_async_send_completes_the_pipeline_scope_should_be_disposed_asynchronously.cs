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

using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    // PR #4282 review finding #1 - HandlerLifetimeScope.Dispose() only ever disposes its pipeline scope
    // synchronously, and CommandProcessor's async SendAsync/PublishAsync used `using var builder = ...`
    // rather than `await using`, so an async pipeline's own IAmAScope handle was always disposed through
    // its synchronous Dispose(), never through DisposeAsync() - an asymmetry with the mapper/transform
    // side, which was deliberately given a full async disposal path. This proves the async path is now
    // genuinely used end to end: CommandProcessor.SendAsync -> PipelineBuilder<T> -> HandlerLifetimeScope
    // -> the pipeline's own IAmAScope handle.
    public class AsyncPipelineScopeDisposalTests
    {
        [Fact]
        public async Task When_an_async_send_completes_the_pipeline_scope_should_be_disposed_asynchronously()
        {
            // Arrange - a handler factory that hands every pipeline the same recording scope handle
            var scopeHandle = new RecordingDisposalPipelineScope();
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, NoOpCommandHandlerAsync>();
            var handlerFactory = new ScopedHandlerFactoryAsync(_ => new NoOpCommandHandlerAsync(), scopeHandle);

            var commandProcessor = new CommandProcessor(
                registry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                new ResiliencePipelineRegistry<string>(),
                new InMemorySchedulerFactory());

            // Act
            await commandProcessor.SendAsync(new MyCommand());

            // Assert - the async disposal path ran; the synchronous one never did
            Assert.True(scopeHandle.DisposeAsyncWasCalled);
            Assert.False(scopeHandle.DisposeWasCalled);
        }
    }
}
