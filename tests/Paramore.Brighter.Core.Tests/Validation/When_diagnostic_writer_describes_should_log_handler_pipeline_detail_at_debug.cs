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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.Core.Tests.Validation;
public class PipelineDiagnosticWriterHandlerDetailTests
{
    [Test]
    public async Task When_diagnostic_writer_describes_should_log_handler_pipeline_detail_at_debug()
    {
        // Arrange — handler with two before-step attributes (backstop at 5, resilience at 3)
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyDescribableCommand), typeof(MyMisorderedBackstopHandler));
        var pipelineBuilder = new PipelineBuilder<IRequest>(registry);
        var logger = new SpyLogger();
        var writer = new PipelineDiagnosticWriter(logger, pipelineBuilder);
        // Act
        writer.Describe();
        // Assert — Debug-level messages contain handler name with sync/async indicator and pipeline chain
        var debugMessages = logger.DebugEntries.Select(e => e.Message).ToList();
        // Section header
        await Assert.That(debugMessages).Contains(m => m.Contains("Handler Pipelines"));
        // Handler name with sync indicator (MyMisorderedBackstopHandler is sync)
        await Assert.That(debugMessages).Contains(m => m.Contains("MyMisorderedBackstopHandler") && m.Contains("sync"));
        // Pipeline chain shows attributes in step order with arrow separator ending at handler
        // Step 3 (UseResiliencePipeline) is outer, Step 5 (RejectMessageOnError) is inner
        // Format: [Attr(step)] → [Attr(step)] → HandlerName
        await Assert.That(debugMessages).Contains(m => m.Contains("UseResiliencePipeline") && m.Contains("→") && m.Contains("RejectMessageOnError") && m.Contains("→") && m.Contains("MyMisorderedBackstopHandler"));
    }
}