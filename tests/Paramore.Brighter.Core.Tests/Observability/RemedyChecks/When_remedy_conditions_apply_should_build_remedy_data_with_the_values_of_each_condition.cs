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

using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Observability;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Observability.RemedyChecks;

public class RemedyCheckConventionDataTests
{
    [Test]
    public async Task When_remedy_conditions_apply_should_build_remedy_data_with_the_values_of_each_condition()
    {
        //Arrange
        var hung = new Dictionary<string, object> { ["running_seconds"] = 91 };
        var failed = new Dictionary<string, object> { ["count"] = 4 };
        RemedyCondition[] conditions =
        [
            new("hung_sweep", "The sweep has run for 91s", hung),
            new("consecutive_failed_sweeps", "4 consecutive failed sweeps", failed)
        ];

        //Act
        var data = RemedyCheckConvention.Data(RemedyKind.Investigate, conditions);

        //Assert
        await Assert.That(data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That((((IEnumerable<string>)(((IEnumerable<string>)(await Assert.That(data[RemedyCheckConvention.ConditionsKey]).IsAssignableTo<IEnumerable<string>>()))))).ToArray()).IsEqualTo(new[] { "hung_sweep", "consecutive_failed_sweeps" });
        await Assert.That(data["hung_sweep"]).IsSameReferenceAs(hung);
        await Assert.That(data["consecutive_failed_sweeps"]).IsSameReferenceAs(failed);
    }
}
