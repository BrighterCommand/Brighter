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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class PipelineDiagnosticWriterSummaryTests
{
    [Fact]
    public void When_diagnostic_writer_describes_should_log_summary_at_information()
    {
        // Arrange — two handler pipelines registered, one publication
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyDescribableCommand), typeof(MyPublicSyncHandler));
        registry.Add(typeof(MyDescribableCommand), typeof(MyPublicAsyncHandler));
        var pipelineBuilder = new PipelineBuilder<IRequest>(registry);
        PipelineBuilder<IRequest>.ClearPipelineCache();

        var publications = new[]
        {
            new Publication { Topic = new RoutingKey("topic.one"), RequestType = typeof(MyDescribableCommand) }
        };

        var logger = new SpyLogger();

        var writer = new PipelineDiagnosticWriter(logger, pipelineBuilder, publications: publications);

        // Act
        writer.Describe();

        // Assert — one Information-level summary line with correct counts
        var infoMessages = logger.InformationEntries.ToList();
        Assert.Single(infoMessages);
        Assert.Contains("2 handler pipeline", infoMessages[0].Message);
        Assert.Contains("1 publication", infoMessages[0].Message);
    }
}
