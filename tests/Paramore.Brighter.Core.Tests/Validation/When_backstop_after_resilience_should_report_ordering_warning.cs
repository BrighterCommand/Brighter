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
using Paramore.Brighter.Policies.Attributes;
using Paramore.Brighter.Policies.Handlers;
using Paramore.Brighter.Reject.Attributes;
using Paramore.Brighter.Reject.Handlers;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class BackstopResilienceValidationTests
{
    [Fact]
    public void When_backstop_after_resilience_should_report_ordering_warning()
    {
        // Arrange — backstop at step 5, resilience at step 3
        // Lower step = outer wrapper, so backstop (step 5) is inner and will never catch failures
        var description = new HandlerPipelineDescription(
            requestType: typeof(MyCommand),
            handlerType: typeof(MyCommandHandler),
            isAsync: false,
            beforeSteps:
            [
                new PipelineStepDescription(
                    typeof(RejectMessageOnErrorAttribute),
                    typeof(RejectMessageOnErrorHandler<>),
                    Step: 5,
                    HandlerTiming.Before),
                new PipelineStepDescription(
                    typeof(UseResiliencePipelineAttribute),
                    typeof(ResilienceExceptionPolicyHandler<>),
                    Step: 3,
                    HandlerTiming.Before)
            ],
            afterSteps: []);

        var spec = HandlerPipelineValidationRules.BackstopAttributeOrdering();

        // Act
        var satisfied = spec.IsSatisfiedBy(description);
        var collector = new ValidationResultCollector<HandlerPipelineDescription>();
        var results = spec.Accept(collector).Where(r => !r.Success).ToList();

        // Assert
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Error!.Severity);
        Assert.Contains("RejectMessageOnErrorAttribute", results[0].Error!.Message);
        Assert.Contains("step 5", results[0].Error!.Message);
        Assert.Contains("UseResiliencePipelineAttribute", results[0].Error!.Message);
        Assert.Contains("step 3", results[0].Error!.Message);
    }
}
