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
using Paramore.Brighter.ServiceActivator.Validation;
using Paramore.Brighter.Validation;
using Xunit;
using AlphaBus = Paramore.Brighter.Core.Tests.Validation.TestDoubles.AlphaBus;
using BetaBus = Paramore.Brighter.Core.Tests.Validation.TestDoubles.BetaBus;

namespace Paramore.Brighter.Core.Tests.Validation;

public class EvaluatingTheSameConfigurationTwiceProducesIdenticalOrderedFindingsTests
{
    [Fact]
    public void When_evaluating_the_same_configuration_twice_should_produce_identical_ordered_findings()
    {
        // Arrange — two mismatched subscriptions, sub-a then sub-b, against a combined factory whose
        // three inner factories match neither declared type
        var combinedFactory = new CombinedChannelFactory(
        [
            new DerivedChannelFactory(),
            new AlphaBus.ChannelFactory(),
            new BetaBus.ChannelFactory()
        ]);

        var subA = new DeclaringSubscription(subscriptionName: new SubscriptionName("sub-a"));
        var subB = new NonMatchingSubscription(subscriptionName: new SubscriptionName("sub-b"));
        var subscriptions = new Subscription[] { subA, subB };

        var consumerSpecs = new ISpecification<Subscription>[]
        {
            ConsumerValidationRules.ChannelFactoryCompatible(combinedFactory)
        };

        // driven through PipelineValidator, not by calling IsSatisfiedBy per subscription from the
        // test — that would impose the order this test then asserts
        var pipelineBuilder = new PipelineBuilder<IRequest>(new SubscriberRegistry());
        PipelineBuilder<IRequest>.ClearPipelineCache();
        var validator = new PipelineValidator(pipelineBuilder, publications: null, subscriptions, consumerSpecs);

        // Act — evaluate the same configuration twice
        var firstRun = validator.Validate();
        var secondRun = validator.Validate();

        // Assert — each run produces exactly two findings, one per subscription, in order sub-a, sub-b
        var firstErrors = firstRun.Errors.ToList();
        var secondErrors = secondRun.Errors.ToList();

        Assert.Equal(2, firstErrors.Count);
        Assert.Equal("Subscription 'sub-a'", firstErrors[0].Source);
        Assert.Equal("Subscription 'sub-b'", firstErrors[1].Source);

        Assert.Equal(2, secondErrors.Count);
        Assert.Equal("Subscription 'sub-a'", secondErrors[0].Source);
        Assert.Equal("Subscription 'sub-b'", secondErrors[1].Source);

        // Assert — the messages are byte-identical across the two runs
        Assert.Equal(firstErrors[0].Message, secondErrors[0].Message);
        Assert.Equal(firstErrors[1].Message, secondErrors[1].Message);
    }
}
