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

public class ValidPublicationValidationTests
{
    [Fact]
    public void When_publication_valid_should_report_no_findings()
    {
        // Arrange — publication with a valid RequestType that implements IRequest
        var publication = new Publication
        {
            Topic = new RoutingKey("test.topic"),
            RequestType = typeof(MyCommand)
        };

        var requestTypeSet = ProducerValidationRules.PublicationRequestTypeSet();
        var implementsIRequest = ProducerValidationRules.PublicationRequestTypeImplementsIRequest();

        // Act
        var allSatisfied = requestTypeSet.IsSatisfiedBy(publication)
            && implementsIRequest.IsSatisfiedBy(publication);

        var collector = new ValidationResultCollector<Publication>();
        var allFailures = requestTypeSet.Accept(collector)
            .Concat(implementsIRequest.Accept(collector))
            .Where(r => !r.Success)
            .ToList();

        // Assert
        Assert.True(allSatisfied);
        Assert.Empty(allFailures);
    }
}
