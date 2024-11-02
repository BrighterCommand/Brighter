using Paramore.Brighter.Core.Tests.Mediator.TestDoubles;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.MediatorWorkflow;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorTwoStepFlowTests  
{
    private readonly MyCommandHandler _myCommandHandler;
    private readonly MediatorWorkflow.Mediator _mediator;

    public MediatorTwoStepFlowTests()
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
                new RequestAndReplyAction<MyCommand, MyEvent>(
                    (state) => new MyCommand{Value = (state.Bag["MyValue"] as string)!},
                    (reply, state) => state.Bag.Add("MyReply", ((MyEvent) reply).Data)),
                "Test", 
                false)], 
            commandProcessor, 
            stateStore
        );
    }
    
    [Fact]
    public void When_running_a_workflow_with_reply()
    {
        
    }
}
