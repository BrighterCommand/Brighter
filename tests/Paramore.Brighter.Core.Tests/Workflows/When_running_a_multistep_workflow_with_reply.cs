using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.MediatorWorkflow;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorReplyMultiStepFlowTests  
{
    private readonly Mediator<WorkflowTestData> _mediator;
    private bool _stepCompletedOne;
    private bool _stepCompletedTwo;
    private readonly Workflow<WorkflowTestData> _flow;

    public MediatorReplyMultiStepFlowTests()
    {
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

        var stepTwo = new Sequence<WorkflowTestData>(
            "Test of Workflow SequenceStep Two",
            new FireAndForget<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (workflowData.Bag["MyValue"] as string)! }),
            () => { _stepCompletedTwo = true; },
            null);
        
         Sequence<WorkflowTestData> stepOne = new(
             "Test of Workflow SequenceStep One",
            new RequestAndReaction<MyCommand, MyEvent, WorkflowTestData>(
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
    
    [Fact]
    public void When_running_a_workflow_with_reply()
    {
        MyCommandHandler.ReceivedCommands.Clear();
        MyEventHandler.ReceivedEvents.Clear();
        
        _mediator.RunWorkFlow(_flow);
        _stepCompletedOne.Should().BeTrue();
        _stepCompletedTwo.Should().BeTrue();
        
        MyCommandHandler.ReceivedCommands.Any(c => c.Value == "Test").Should().BeTrue(); 
        MyEventHandler.ReceivedEvents.Any(e => e.Value == "Test").Should().BeTrue();
        _flow.State.Should().Be(WorkflowState.Done);
    }
}
