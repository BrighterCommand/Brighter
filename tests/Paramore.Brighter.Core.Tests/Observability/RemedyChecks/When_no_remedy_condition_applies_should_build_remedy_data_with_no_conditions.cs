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

using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.RemedyChecks;

public class RemedyCheckConventionEmptyDataTests
{
    [Fact]
    public void When_no_remedy_condition_applies_should_build_remedy_data_with_no_conditions()
    {
        //Arrange
        //Act
        var data = RemedyCheckConvention.Data(RemedyKind.Restart);
        data["extra"] = 1;

        //Assert
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(data[RemedyCheckConvention.ConditionsKey]));
        Assert.Equal(1, data["extra"]);
    }

    [Fact]
    public void When_the_remedy_kind_is_undefined_should_reject_it_when_building_remedy_data()
    {
        //Arrange
        var undefined = (RemedyKind)99;

        //Act
        var withoutConditions = Record.Exception(() => RemedyCheckConvention.Data(undefined));
        var withConditions = Record.Exception(() => RemedyCheckConvention.Data(undefined, Array.Empty<RemedyCondition>()));

        //Assert
        Assert.IsType<ArgumentOutOfRangeException>(withoutConditions);
        Assert.IsType<ArgumentOutOfRangeException>(withConditions);
    }
}
