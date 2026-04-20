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

public class BackstopBeforeResilienceValidationTests
{
    [Fact]
    public void When_backstop_before_resilience_should_pass()
    {
        // Arrange — backstop at step 0, resilience at step 1
        // Lower step = outer wrapper, so backstop (step 0) is outermost — correct ordering
        var description = new HandlerPipelineDescription(
            requestType: typeof(MyCommand),
            handlerType: typeof(MyCommandHandler),
            isAsync: false,
            beforeSteps:
            [
                new PipelineStepDescription(
                    typeof(RejectMessageOnErrorAttribute),
                    typeof(RejectMessageOnErrorHandler<>),
                    Step: 0,
                    HandlerTiming.Before),
                new PipelineStepDescription(
                    typeof(UseResiliencePipelineAttribute),
                    typeof(ResilienceExceptionPolicyHandler<>),
                    Step: 1,
                    HandlerTiming.Before)
            ],
            afterSteps: []);

        var spec = HandlerPipelineValidationRules.BackstopAttributeOrdering();

        // Act
        var satisfied = spec.IsSatisfiedBy(description);
        var collector = new ValidationResultCollector<HandlerPipelineDescription>();
        var results = spec.Accept(collector).Where(r => !r.Success).ToList();

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }
}
