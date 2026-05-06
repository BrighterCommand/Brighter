using Paramore.Brighter.Core.Tests.Specifications.TestDoubles;

namespace Paramore.Brighter.Core.Tests.Specifications;
public class SpecificationTests
{
    [Test]
    public async Task When_evaluating_a_specificaion()
    {
        var specification = new Specification<SpecificationTestState>(state => state.State == SpecificationState.Done);
        await Assert.That(specification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Done })).IsTrue();
        await Assert.That(specification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Ready })).IsFalse();
    }

    [Test]
    public async Task When_combining_specifications_with_and()
    {
        var doneSpecification = new Specification<SpecificationTestState>(state => state.State == SpecificationState.Done);
        var runningSpecification = new Specification<SpecificationTestState>(state => state.State == SpecificationState.Running);
        var combinedSpecification = doneSpecification.And(runningSpecification);
        await Assert.That(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Done })).IsFalse();
        await Assert.That(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Running })).IsFalse();
    }

    [Test]
    public async Task When_combining_specifications_with_or()
    {
        var doneSpecification = new Specification<SpecificationTestState>(state => state.State == SpecificationState.Done);
        var runningSpecification = new Specification<SpecificationTestState>(state => state.State == SpecificationState.Running);
        var combinedSpecification = doneSpecification.Or(runningSpecification);
        await Assert.That(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Done })).IsTrue();
        await Assert.That(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Running })).IsTrue();
        await Assert.That(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Ready })).IsFalse();
    }

    [Test]
    public async Task When_negating_a_specification()
    {
        var doneSpecification = new Specification<SpecificationTestState>(state => state.State == SpecificationState.Done);
        var notDoneSpecification = doneSpecification.Not();
        await Assert.That(notDoneSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Done })).IsFalse();
        await Assert.That(notDoneSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Ready })).IsTrue();
    }

    [Test]
    public async Task When_combining_specifications_with_and_not()
    {
        var doneSpecification = new Specification<SpecificationTestState>(state => state.State == SpecificationState.Done);
        var runningSpecification = new Specification<SpecificationTestState>(state => state.State == SpecificationState.Running);
        var combinedSpecification = doneSpecification.AndNot(runningSpecification);
        await Assert.That(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Done })).IsTrue();
        await Assert.That(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Running })).IsFalse();
    }

    [Test]
    public async Task When_combining_specifications_with_or_not()
    {
        var doneSpecification = new Specification<SpecificationTestState>(state => state.State == SpecificationState.Done);
        var runningSpecification = new Specification<SpecificationTestState>(state => state.State == SpecificationState.Running);
        var combinedSpecification = doneSpecification.OrNot(runningSpecification);
        await Assert.That(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Done })).IsTrue();
        await Assert.That(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Ready })).IsTrue();
        await Assert.That(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = SpecificationState.Running })).IsFalse();
    }
}