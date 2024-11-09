using System;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.MediatorWorkflow;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorChoiceFlowTests 
{
    private readonly Mediator<WorkflowTestData>? _mediator;
    private readonly Workflow<WorkflowTestData> _flow;
    private bool _stepCompletedTwo;
    private bool _stepCompletedOne;

    public MediatorChoiceFlowTests(bool stepCompletedTwo)
    {
        _stepCompletedTwo = stepCompletedTwo;
        // arrange
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        registry.Register<MyEvent, MyEventHandler>();

        IAmACommandProcessor? commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactorySync((handlerType) =>
            handlerType switch
            { 
                _ when handlerType == typeof(MyCommandHandler) => new MyCommandHandler(commandProcessor),
                _ when handlerType == typeof(MyEventHandler) => new MyEventHandler(_mediator),
                _ => throw new InvalidOperationException($"The handler type {handlerType} is not supported")
            });

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        var workflowData= new WorkflowTestData();
        workflowData.Bag.Add("MyValue", "Test");

        var stepTwo = new Step<WorkflowTestData>("Test of Workflow Step Two",
            new FireAndForgetAction<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (workflowData.Bag["MyValue"] as string)! }),
            () => { _stepCompletedTwo = true; },
            null);
        
        Step<WorkflowTestData> stepOne = new("Test of Workflow Step One",
            new RequestAndReplyAction<MyCommand, MyEvent, WorkflowTestData>(
                () => new MyCommand { Value = (workflowData.Bag["MyValue"] as string)! },
                (reply) => workflowData.Bag.Add("MyReply", ((MyEvent)reply).Value)),
            () => { _stepCompletedOne = true; },
            stepTwo);
       
        _flow = new Workflow<WorkflowTestData>(stepOne, workflowData) ;
        
        _mediator = new Mediator<WorkflowTestData>(
            commandProcessor,
            new InMemoryWorkflowStore()
        );
    }
    
    public void When_running_a_choice_workflow_step()
    {
 

        // act
        _mediator.RunWorkFlow(_flow);

        // assert
        _stepCompletedOne.Should().BeTrue();
        _stepCompletedTwo.Should().BeTrue();
    }
}
