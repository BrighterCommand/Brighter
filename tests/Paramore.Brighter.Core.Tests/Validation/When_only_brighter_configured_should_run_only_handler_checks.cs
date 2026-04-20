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
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class PipelineValidatorHandlerOnlyTests
{
    [Fact]
    public void When_only_brighter_configured_should_run_only_handler_checks()
    {
        // Arrange — internal handler triggers a handler-path error;
        //           no publications or subscriptions provided
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyDescribableCommand), typeof(MyInternalHandler));
        var pipelineBuilder = new PipelineBuilder<IRequest>(registry);
        PipelineBuilder<IRequest>.ClearPipelineCache();

        var validator = new PipelineValidator(pipelineBuilder);

        // Act
        var result = validator.Validate();

        // Assert — only handler-path errors appear
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("not public"));

        // No producer or consumer errors
        Assert.DoesNotContain(result.Errors, e => e.Message.Contains("RequestType"));
        Assert.DoesNotContain(result.Errors, e => e.Message.Contains("No handler registered"));
    }
}
