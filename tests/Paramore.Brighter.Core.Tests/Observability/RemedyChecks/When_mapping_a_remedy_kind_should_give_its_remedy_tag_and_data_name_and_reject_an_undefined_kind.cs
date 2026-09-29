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
using Paramore.Brighter.Observability;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Observability.RemedyChecks;

public class RemedyCheckConventionTests
{
    [Test]
    public async Task When_mapping_a_remedy_kind_should_give_its_remedy_tag_and_data_name_and_reject_an_undefined_kind_tags()
    {
        //Arrange
        //Act
        var restart = RemedyCheckConvention.Tag(RemedyKind.Restart);
        var takeOutOfService = RemedyCheckConvention.Tag(RemedyKind.TakeOutOfService);
        var investigate = RemedyCheckConvention.Tag(RemedyKind.Investigate);

        //Assert
        await Assert.That(restart).IsEqualTo("brighter-restart");
        await Assert.That(restart).IsEqualTo(RemedyCheckConvention.RestartTag);
        await Assert.That(takeOutOfService).IsEqualTo("brighter-take-out-of-service");
        await Assert.That(takeOutOfService).IsEqualTo(RemedyCheckConvention.TakeOutOfServiceTag);
        await Assert.That(investigate).IsEqualTo("brighter-investigate");
        await Assert.That(investigate).IsEqualTo(RemedyCheckConvention.InvestigateTag);
    }

    [Test]
    public async Task When_mapping_a_remedy_kind_should_give_its_remedy_tag_and_data_name_and_reject_an_undefined_kind_names_and_keys()
    {
        //Arrange
        //Act
        var restart = RemedyCheckConvention.Name(RemedyKind.Restart);
        var takeOutOfService = RemedyCheckConvention.Name(RemedyKind.TakeOutOfService);
        var investigate = RemedyCheckConvention.Name(RemedyKind.Investigate);

        //Assert
        await Assert.That(restart).IsEqualTo("restart");
        await Assert.That(takeOutOfService).IsEqualTo("take_out_of_service");
        await Assert.That(investigate).IsEqualTo("investigate");
        await Assert.That(RemedyCheckConvention.RemedyKindKey).IsEqualTo("remedy_kind");
        await Assert.That(RemedyCheckConvention.ConditionsKey).IsEqualTo("conditions");
    }

    [Test]
    public async Task When_mapping_a_remedy_kind_should_give_its_remedy_tag_and_data_name_and_reject_an_undefined_kind_undefined_kind()
    {
        //Arrange
        var undefined = (RemedyKind)99;

        //Act
        var tag = TestExceptionRecorder.Capture(() => RemedyCheckConvention.Tag(undefined));
        var name = TestExceptionRecorder.Capture(() => RemedyCheckConvention.Name(undefined));

        //Assert
        await Assert.That(tag).IsTypeOf<ArgumentOutOfRangeException>();
        await Assert.That(name).IsTypeOf<ArgumentOutOfRangeException>();
    }
}
