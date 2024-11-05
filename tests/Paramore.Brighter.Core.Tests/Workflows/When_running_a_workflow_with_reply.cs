using System;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.MediatorWorkflow;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorReplyStepFlowTests  
{
    private readonly Mediator<WorkflowTestData> _mediator;
    private bool _stepCompleted;

    public MediatorReplyStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        registry.Register<MyEvent, MyEventHandler>();
        var handlerFactory = new SimpleHandlerFactorySync((handlerType) =>
             handlerType switch
            { 
                _ when handlerType == typeof(MyCommandHandler) => new MyCommandHandler(),
                _ when handlerType == typeof(MyEventHandler) => new MyEventHandler(_mediator),
                _ => throw new InvalidOperationException($"The handler type {handlerType} is not supported")
            });

        var commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(),
            new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        var workflowData= new WorkflowTestData();
        workflowData.Bag.Add("MyValue", "Test");
        
        var flow = new Workflow<WorkflowTestData>(workflowData) ;

        var step = new Step<WorkflowTestData>("Test of Workflow",
            new RequestAndReplyAction<MyCommand, MyEvent, WorkflowTestData>(
                () => new MyCommand { Value = (flow.Data.Bag["MyValue"] as string)! },
                (reply) => flow.Data.Bag.Add("MyReply", ((MyEvent)reply).Data)),
            () => { _stepCompleted = true; },
            flow,
            null);
        
        _mediator = new Mediator<WorkflowTestData>(
            commandProcessor,
            new InMemoryWorkflowStore()
        );
    }
    
    [Fact]
    public void When_running_a_workflow_with_reply()
    {
        
    }
}
