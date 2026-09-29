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


namespace Paramore.Brighter.Core.Tests.CommandProcessors;

public class RequestContextCopyResilienceTests
{
    [Test]
    public async System.Threading.Tasks.Task When_copying_request_context_should_omit_execution_state_but_preserve_resilience_pipelines()
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
            await Assert.That(copy).IsNotSameReferenceAs(original);
            await Assert.That(copy.ResilienceContext).IsNull();
            await Assert.That(copy.ResiliencePipeline).IsSameReferenceAs(pipelines);
            await Assert.That(copy.Bag).IsNotSameReferenceAs(original.Bag);
            await Assert.That(copy.Bag["input"]).IsEqualTo("original-value");
            await Assert.That(original.Bag.Keys).DoesNotContain("copy-only");
            await Assert.That(original.ResilienceContext).IsSameReferenceAs(supplied);
            await Assert.That(supplied.OperationKey).IsEqualTo("caller-operation");
            await Assert.That(supplied.Properties.GetValue(propertyKey, "")).IsEqualTo("caller-value");
            await Assert.That(supplied.CancellationToken).IsEqualTo(cancellation.Token);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(supplied);
        }
    }
}
