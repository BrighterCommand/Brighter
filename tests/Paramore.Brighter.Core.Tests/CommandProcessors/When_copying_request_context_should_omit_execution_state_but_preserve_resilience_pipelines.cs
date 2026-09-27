#region Licence
/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

#nullable enable

using System.Threading;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors;

public class RequestContextCopyResilienceTests
{
    [Fact]
    public void When_copying_request_context_should_omit_execution_state_but_preserve_resilience_pipelines()
    {
        //Arrange
        using var pipelines = new ResiliencePipelineRegistry<string>();
        using var cancellation = new CancellationTokenSource();
        var propertyKey = new ResiliencePropertyKey<string>("caller-data");
        var supplied = ResilienceContextPool.Shared.Get("caller-operation", cancellation.Token);
        supplied.Properties.Set(propertyKey, "caller-value");
        try
        {
            var original = new RequestContext
            {
                ResilienceContext = supplied,
                ResiliencePipeline = pipelines
            };
            original.Bag["input"] = "original-value";

            //Act
            var copy = original.CreateCopy();
            copy.Bag["copy-only"] = "independent-value";

            //Assert
            Assert.NotSame(original, copy);
            Assert.Null(copy.ResilienceContext);
            Assert.Same(pipelines, copy.ResiliencePipeline);
            Assert.NotSame(original.Bag, copy.Bag);
            Assert.Equal("original-value", copy.Bag["input"]);
            Assert.DoesNotContain("copy-only", original.Bag.Keys);
            Assert.Same(supplied, original.ResilienceContext);
            Assert.Equal("caller-operation", supplied.OperationKey);
            Assert.Equal("caller-value", supplied.Properties.GetValue(propertyKey, ""));
            Assert.Equal(cancellation.Token, supplied.CancellationToken);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(supplied);
        }
    }
}
