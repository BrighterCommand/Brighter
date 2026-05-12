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
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class InternalHandlerValidationTests
{
    [Fact]
    public void When_handler_type_is_not_public_should_report_visibility_error()
    {
        // Arrange — an internal handler type is not visible to the pipeline builder
        var description = new HandlerPipelineDescription(
            requestType: typeof(MyCommand),
            handlerType: typeof(InternalTestHandler),
            isAsync: false,
            beforeSteps: [],
            afterSteps: []);

        var spec = HandlerPipelineValidationRules.HandlerTypeVisibility();

        // Act
        var satisfied = spec.IsSatisfiedBy(description);
        var collector = new ValidationResultCollector<HandlerPipelineDescription>();
        var results = spec.Accept(collector).ToList();

        // Assert
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Error, results[0].Error!.Severity);
        Assert.Contains("not public", results[0].Error!.Message);
        Assert.Contains(nameof(InternalTestHandler), results[0].Error!.Source);
    }
}

/// <summary>Internal handler type for testing visibility validation.</summary>
internal class InternalTestHandler : RequestHandler<MyCommand>
{
    public override MyCommand Handle(MyCommand command) => base.Handle(command);
}
