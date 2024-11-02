using System.Collections.Generic;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.Mediator.TestDoubles;
using Paramore.Brighter.MediatorWorkflow;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorOneStepFlowTests 
{
    private readonly MyCommandHandler _myCommandHandler;
    private readonly MediatorWorkflow.Mediator _mediator;

    public MediatorOneStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        _myCommandHandler = new MyCommandHandler();
        var handlerFactory = new SimpleHandlerFactorySync(_ => _myCommandHandler);

        var commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();    
        
        var stateStore = new InMemoryStateStore();
        _mediator = new MediatorWorkflow.Mediator(
            [new Step("Test of Workflow", 
                new FireAndForgetAction<MyCommand>((state) => new MyCommand{Value = (state.Bag["MyValue"] as string)!}), 
                "Test", 
                false)], 
            commandProcessor, 
            stateStore
            );
    }
    
    [Fact]
    public void When_running_a_single_step_workflow()
    {
        _mediator.InitializeWorkflow(new WorkflowState() {Bag = new Dictionary<string, object> {{"MyValue", "Test"}}});
        _mediator.RunWorkFlow();
        
        _myCommandHandler.ReceivedCommand?.Value.Should().Be( "Test");    
    }
}
