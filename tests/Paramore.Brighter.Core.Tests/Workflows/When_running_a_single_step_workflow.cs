using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.MediatorWorkflow;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorOneStepFlowTests 
{
    private readonly Mediator<WorkflowTestData> _mediator;
    private readonly Step<WorkflowTestData> _step;

    public MediatorOneStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();

        CommandProcessor commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyCommandHandler(commandProcessor));

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();    
        
        var workflowData= new WorkflowTestData();
        workflowData.Bag.Add("MyValue", "Test");
        
        var flow = new Workflow<WorkflowTestData>(workflowData) ;
        
        _mediator = new Mediator<WorkflowTestData>(
            commandProcessor, 
            new InMemoryWorkflowStore() 
            );

        _step = new Step<WorkflowTestData>("Test of Workflow",
            new FireAndForgetAction<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (flow.Data.Bag["MyValue"] as string)! }),
            () => { },
            flow, 
            null
            );
    }
    
    [Fact]
    public void When_running_a_single_step_workflow()
    {
        MyCommandHandler.ReceivedCommands.Clear();
        
        _mediator.RunWorkFlow(_step);
        
        MyCommandHandler.ReceivedCommands.Any(c => c.Value == "Test").Should().BeTrue();
    }
}
