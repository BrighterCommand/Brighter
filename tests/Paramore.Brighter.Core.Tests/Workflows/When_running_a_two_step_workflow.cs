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
    private readonly Workflow<WorkflowTestData> _flow;

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
        
        
        var secondStep = new Step<WorkflowTestData>("Test of Workflow Two",
            new FireAndForget<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (workflowData.Bag["MyValue"] as string)! }),
            () => { },
            null
            );
        
        var firstStep = new Step<WorkflowTestData>("Test of Workflow One",
            new FireAndForget<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (workflowData.Bag["MyValue"] as string)! }),
            () => { workflowData.Bag["MyValue"] = "TestTwo"; }, 
            secondStep
            );
        
        _mediator = new Mediator<WorkflowTestData>(
            commandProcessor,
            new InMemoryWorkflowStore()
            );

        _flow = new Workflow<WorkflowTestData>(firstStep, workflowData);
    }
    
    [Fact]
    public void When_running_a_single_step_workflow()
    {
        MyCommandHandler.ReceivedCommands.Clear();
        
        _mediator.RunWorkFlow(_flow);
        
        MyCommandHandler.ReceivedCommands.Any(c => c.Value == "Test").Should().BeTrue();
        MyCommandHandler.ReceivedCommands.Any(c => c.Value == "TestTwo").Should().BeTrue();
        _flow.State.Should().Be(WorkflowState.Done);
    }
}
