using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.MediatorWorkflow;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorChangeStepFlowTests 
{
    private readonly Mediator<WorkflowTestData> _mediator;
    private readonly Workflow<WorkflowTestData> _flow;
    private readonly FakeTimeProvider _timeProvider;
    private bool _stepCompleted;

    public MediatorChangeStepFlowTests ()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();

        CommandProcessor commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyCommandHandler(commandProcessor));

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();    
        
        var workflowData= new WorkflowTestData();
        workflowData.Bag.Add("MyValue", "Test");
        
        _timeProvider = new FakeTimeProvider();
        
        var firstStep = new Sequence<WorkflowTestData>(
            "Test of Workflow",
            new Change<WorkflowTestData>( (flow) =>
            {
                flow.Bag["MyValue"] = "Altered";
                return flow;
            }),
            () => { _stepCompleted = true; },
            null
            );
        
        _flow = new Workflow<WorkflowTestData>(firstStep, workflowData) ;
        
        _mediator = new Mediator<WorkflowTestData>(
            commandProcessor, 
            new InMemoryWorkflowStore() 
            );
    }
    
    [Fact]
    public void When_running_a_single_step_workflow()
    {
        //We won't really see th block in action as the test will simply block for 500ms
        _mediator.RunWorkFlow(_flow);
        
        _flow.State.Should().Be(WorkflowState.Done);
        _stepCompleted.Should().BeTrue();
        _flow.Bag["MyValue"].Should().Be("Altered");
    }
}
