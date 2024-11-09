using Paramore.Brighter.MediatorWorkflow;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Specifications;

public class SpecificationTests
{
    [Fact]
    public void When_evaluating_a_specificaion()
    {
        var specification = new Specification<WorkflowState>(state => state == WorkflowState.Done);
        Assert.True(specification.IsSatisfiedBy(WorkflowState.Done));
        Assert.False(specification.IsSatisfiedBy(WorkflowState.Ready));
    }

    [Fact]
    public void When_combining_specifications_with_and()
    {
        var doneSpecification = new Specification<WorkflowState>(state => state == WorkflowState.Done);
        var runningSpecification = new Specification<WorkflowState>(state => state == WorkflowState.Running);
        var combinedSpecification = doneSpecification.And(runningSpecification);

        Assert.False(combinedSpecification.IsSatisfiedBy(WorkflowState.Done));
        Assert.False(combinedSpecification.IsSatisfiedBy(WorkflowState.Running));
    }

    [Fact]
    public void When_combining_specifications_with_or()
    {
        var doneSpecification = new Specification<WorkflowState>(state => state == WorkflowState.Done);
        var runningSpecification = new Specification<WorkflowState>(state => state == WorkflowState.Running);
        var combinedSpecification = doneSpecification.Or(runningSpecification);

        Assert.True(combinedSpecification.IsSatisfiedBy(WorkflowState.Done));
        Assert.True(combinedSpecification.IsSatisfiedBy(WorkflowState.Running));
        Assert.False(combinedSpecification.IsSatisfiedBy(WorkflowState.Ready));
    }

    [Fact]
    public void When_negating_a_specification()
    {
        var doneSpecification = new Specification<WorkflowState>(state => state == WorkflowState.Done);
        var notDoneSpecification = doneSpecification.Not();

        Assert.False(notDoneSpecification.IsSatisfiedBy(WorkflowState.Done));
        Assert.True(notDoneSpecification.IsSatisfiedBy(WorkflowState.Ready));
    }

    [Fact]
    public void When_combining_specifications_with_and_not()
    {
        var doneSpecification = new Specification<WorkflowState>(state => state == WorkflowState.Done);
        var runningSpecification = new Specification<WorkflowState>(state => state == WorkflowState.Running);
        var combinedSpecification = doneSpecification.AndNot(runningSpecification);

        Assert.True(combinedSpecification.IsSatisfiedBy(WorkflowState.Done));
        Assert.False(combinedSpecification.IsSatisfiedBy(WorkflowState.Running));
    }

    [Fact]
    public void When_combining_specifications_with_or_not()
    {
        var doneSpecification = new Specification<WorkflowState>(state => state == WorkflowState.Done);
        var runningSpecification = new Specification<WorkflowState>(state => state == WorkflowState.Running);
        var combinedSpecification = doneSpecification.OrNot(runningSpecification);

        Assert.True(combinedSpecification.IsSatisfiedBy(WorkflowState.Done));
        Assert.True(combinedSpecification.IsSatisfiedBy(WorkflowState.Ready));
        Assert.False(combinedSpecification.IsSatisfiedBy(WorkflowState.Running));
    }
}
    
    
    
