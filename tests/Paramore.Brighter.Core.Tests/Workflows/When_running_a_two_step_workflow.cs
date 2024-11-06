using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.MediatorWorkflow;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorTwoStepFlowTests 
{
    private readonly Mediator<WorkflowTestData> _mediator;
    private readonly Step<WorkflowTestData> _stepOne;

    public MediatorTwoStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        
        CommandProcessor commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyCommandHandler(commandProcessor));

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();    
        
        var workflowData= new WorkflowTestData();
        workflowData.Bag.Add("MyValue", "Test");
        
        var flow = new Workflow<WorkflowTestData>(workflowData);
        
        _mediator = new Mediator<WorkflowTestData>(
            commandProcessor,
            new InMemoryWorkflowStore()
            );

        
        var stepTwo = new Step<WorkflowTestData>("Test of Workflow Two",
            new FireAndForgetAction<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (flow.Data.Bag["MyValue"] as string)! }),
            () => { },
            flow,
            null
            );
        
        _stepOne = new Step<WorkflowTestData>("Test of Workflow One",
            new FireAndForgetAction<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (flow.Data.Bag["MyValue"] as string)! }),
            () => { flow.Data.Bag["MyValue"] = "TestTwo"; }, 
            flow,
            stepTwo
            );
    }
    
    [Fact]
    public void When_running_a_single_step_workflow()
    {
        MyCommandHandler.ReceivedCommands.Clear();
        
        _mediator.RunWorkFlow(_stepOne);
        
        MyCommandHandler.ReceivedCommands.Any(c => c.Value == "Test").Should().BeTrue();
        MyCommandHandler.ReceivedCommands.Any(c => c.Value == "TestTwo").Should().BeTrue();
    }
}
