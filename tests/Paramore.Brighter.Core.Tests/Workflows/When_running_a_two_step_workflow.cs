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
    private readonly Mediator _mediator;
    private readonly Step _stepOne;

    public MediatorTwoStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        MyCommandHandler myCommandHandler = new();
        var handlerFactory = new SimpleHandlerFactorySync(_ => myCommandHandler);

        var commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();    
        
        var flow = new Workflow() {Bag = new Dictionary<string, object> {{"MyValue", "Test"}}};
        
        _mediator = new Mediator(
            commandProcessor 
            );

        
        var stepTwo = new Step("Test of Workflow Two",
            new FireAndForgetAction<MyCommand>(() => new MyCommand { Value = (flow.Bag["MyValue"] as string)! }),
            () => { },
        flow,
            null
            );
        
        _stepOne = new Step("Test of Workflow One",
            new FireAndForgetAction<MyCommand>(() => new MyCommand { Value = (flow.Bag["MyValue"] as string)! }),
            () => { flow.Bag["MyValue"] = "TestTwo"; }, 
            flow,
            stepTwo
            );
    }
    
    [Fact]
    public void When_running_a_single_step_workflow()
    {
        _mediator.RunWorkFlow(_stepOne);
        
        MyCommandHandler.ReceivedCommands.Any(c => c.Value == "Test").Should().BeTrue();
        MyCommandHandler.ReceivedCommands.Any(c => c.Value == "TestTwo").Should().BeTrue();
    }
}
