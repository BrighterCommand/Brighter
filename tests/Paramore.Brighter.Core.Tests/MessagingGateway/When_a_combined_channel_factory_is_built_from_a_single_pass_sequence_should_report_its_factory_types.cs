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
using Paramore.Brighter.Core.Tests.MessagingGateway.TestDoubles;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;

namespace Paramore.Brighter.Core.Tests.MessagingGateway;

public class CombinedChannelFactoryFactoryTypesTests
{
    [Test]
    public async System.Threading.Tasks.Task When_a_combined_channel_factory_is_built_from_a_single_pass_sequence_should_report_its_factory_types()
    {
        // Arrange — a sequence that throws on a second GetEnumerator() call, so FactoryTypes must
        // be derived from the already-materialised _factories field, not by re-enumerating factories
        var sequence = new SinglePassChannelFactorySequence(
            new DeclaredChannelFactory(),
            new NonMatchingChannelFactory());
        var combinedChannelFactory = new CombinedChannelFactory(sequence);

        // Act
        var factoryTypes = combinedChannelFactory.FactoryTypes;

        // Assert — inner factory types, in constructor order
        await Assert.That(factoryTypes).IsEquivalentTo([typeof(DeclaredChannelFactory), typeof(NonMatchingChannelFactory)], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
