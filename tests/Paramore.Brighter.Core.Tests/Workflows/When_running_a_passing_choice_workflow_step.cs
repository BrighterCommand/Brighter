using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.MediatorWorkflow;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorPassingChoiceFlowTests 
{
    private readonly Mediator<WorkflowTestData>? _mediator;
    private readonly Workflow<WorkflowTestData> _flow;
    private bool _stepCompletedOne;
    private bool _stepCompletedTwo;
    private bool _stepCompletedThree;

    public MediatorPassingChoiceFlowTests()
    {
        // arrange
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        registry.Register<MyOtherCommand, MyOtherCommandHandler>();

        IAmACommandProcessor? commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactorySync((handlerType) =>
            handlerType switch
            { 
                _ when handlerType == typeof(MyCommandHandler) => new MyCommandHandler(commandProcessor),
                _ when handlerType == typeof(MyOtherCommandHandler) => new (commandProcessor),
                _ => throw new InvalidOperationException($"The handler type {handlerType} is not supported")
            });

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        var workflowData= new WorkflowTestData();
        workflowData.Bag.Add("MyValue", "Pass");
        
        var stepThree = new Sequence<WorkflowTestData>(
            "Test of Workflow SequenceStep Three",
            new FireAndForget<MyOtherCommand, WorkflowTestData>(() => new MyOtherCommand { Value = (workflowData.Bag["MyValue"] as string)! }),
            () => { _stepCompletedThree = true; },
            null);
        
        var stepTwo = new Sequence<WorkflowTestData>(
            "Test of Workflow SequenceStep Two",
            new FireAndForget<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (workflowData.Bag["MyValue"] as string)! }),
            () => { _stepCompletedTwo = true; },
            null);

         var stepOne = new ExclusiveChoice<WorkflowTestData>(
             "Test of Workflow SequenceStep One",
             new Specification<WorkflowTestData>(x => x.Bag["MyValue"] as string == "Pass"),
            () => { _stepCompletedOne = true; },
            stepTwo, 
            stepThree);
       
        _flow = new Workflow<WorkflowTestData>(stepOne, workflowData) ;
        
        _mediator = new Mediator<WorkflowTestData>(
            commandProcessor,
            new InMemoryWorkflowStore()
        );
    }
    
    [Fact]
    public void When_running_a_choice_workflow_step()
    {
        MyCommandHandler.ReceivedCommands.Clear();
        MyOtherCommandHandler.ReceivedCommands.Clear();
        
        _mediator?.RunWorkFlow(_flow);

        _stepCompletedOne.Should().BeTrue();
        _stepCompletedTwo.Should().BeTrue();
        _stepCompletedThree.Should().BeFalse();
        MyCommandHandler.ReceivedCommands.Any(c => c.Value == "Pass").Should().BeTrue();
        MyOtherCommandHandler.ReceivedCommands.Any().Should().BeFalse();
        _stepCompletedOne.Should().BeTrue();
    }
}
