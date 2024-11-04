using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.MediatorWorkflow;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorReplyStepFlowTests  
{
    private readonly Mediator _mediator;

    public MediatorReplyStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        registry.Register<MyEvent, MyEventHandler>();
        MyCommandHandler myCommandHandler = new();
        var handlerFactory = new SimpleHandlerFactorySync(_ => myCommandHandler);

        var commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(),
            new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        var flow = new Workflow();

        var step = new Step("Test of Workflow",
            new RequestAndReplyAction<MyCommand, MyEvent>(
                () => new MyCommand { Value = (flow.Bag["MyValue"] as string)! },
                (reply) => flow.Bag.Add("MyReply", ((MyEvent)reply).Data)),
            () => { },
            flow,
            null);
        
        _mediator = new Mediator(
            commandProcessor 
        );
    }
    
    [Fact]
    public void When_running_a_workflow_with_reply()
    {
        
    }
}
