using Paramore.Brighter.Core.Tests.Specifications.TestDoubles;
using Paramore.Brighter.Mediator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Specifications;

public class SpecificationTests
{
    [Fact]
    public void When_evaluating_a_specificaion()
    {
        var specification = new Specification<SpecificationTestState>(state => state.State == TestState.Done);
        Assert.True(specification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Done }));
        Assert.False(specification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Ready }));
    }

    [Fact]
    public void When_combining_specifications_with_and()
    {
        var doneSpecification = new Specification<SpecificationTestState>(state => state.State == TestState.Done);
        var runningSpecification = new Specification<SpecificationTestState>(state => state.State == TestState.Running);
        var combinedSpecification = doneSpecification.And(runningSpecification);

        Assert.False(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Done }));
        Assert.False(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Running }));
    }

    [Fact]
    public void When_combining_specifications_with_or()
    {
        var doneSpecification = new Specification<SpecificationTestState>(state => state.State == TestState.Done);
        var runningSpecification = new Specification<SpecificationTestState>(state => state.State == TestState.Running);
        var combinedSpecification = doneSpecification.Or(runningSpecification);

        Assert.True(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Done }));
        Assert.True(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Running }));
        Assert.False(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Ready }));
    }

    [Fact]
    public void When_negating_a_specification()
    {
        var doneSpecification = new Specification<SpecificationTestState>(state => state.State == TestState.Done);
        var notDoneSpecification = doneSpecification.Not();

        Assert.False(notDoneSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Done }));
        Assert.True(notDoneSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Ready }));
    }

    [Fact]
    public void When_combining_specifications_with_and_not()
    {
        var doneSpecification = new Specification<SpecificationTestState>(state => state.State == TestState.Done);
        var runningSpecification = new Specification<SpecificationTestState>(state => state.State == TestState.Running);
        var combinedSpecification = doneSpecification.AndNot(runningSpecification);

        Assert.True(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Done }));
        Assert.False(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Running }));
    }

    [Fact]
    public void When_combining_specifications_with_or_not()
    {
        var doneSpecification = new Specification<SpecificationTestState>(state => state.State == TestState.Done);
        var runningSpecification = new Specification<SpecificationTestState>(state => state.State == TestState.Running);
        var combinedSpecification = doneSpecification.OrNot(runningSpecification);

        Assert.True(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Done }));
        Assert.True(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Ready }));
        Assert.False(combinedSpecification.IsSatisfiedBy(new SpecificationTestState { State = TestState.Running }));
    }
}
    
    
    
