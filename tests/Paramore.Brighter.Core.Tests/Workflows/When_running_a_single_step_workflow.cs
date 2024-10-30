using Paramore.Brighter.Core.Tests.Mediator.TestDoubles;
using Paramore.Brighter.Workflow;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorOneStepFlowTests 
{
    private readonly MyCommandHandler _myCommandHandler;

    public MediatorOneStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        _myCommandHandler = new MyCommandHandler();
        var handlerFactory = new SimpleHandlerFactorySync(_ => _myCommandHandler);

        var commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();    
        
        var stateStore = new InMemoryStateStore();
        var mediator = new Workflow.Mediator(commandProcessor, stateStore);
    }
    
    [Fact]
    public void When_running_a_single_step_workflow()
    {
        
    }
}
