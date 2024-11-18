using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using Xunit;
using MyCommand = Paramore.Brighter.Core.Tests.Workflows.TestDoubles.MyCommand;
using MyCommandHandlerAsync = Paramore.Brighter.Core.Tests.Workflows.TestDoubles.MyCommandHandlerAsync;
using MyEvent = Paramore.Brighter.Core.Tests.Workflows.TestDoubles.MyEvent;
using MyEventHandlerAsync = Paramore.Brighter.Core.Tests.Workflows.TestDoubles.MyEventHandlerAsync;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorReplyStepFlowTests  
{
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly Job<WorkflowTestData> _job;
    private bool _stepCompleted;

    public MediatorReplyStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        registry.RegisterAsync<MyEvent, MyEventHandlerAsync>();

        IAmACommandProcessor commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync((handlerType) =>
             handlerType switch
            { 
                _ when handlerType == typeof(MyCommandHandlerAsync) => new MyCommandHandlerAsync(commandProcessor),
                _ when handlerType == typeof(MyEventHandlerAsync) => new MyEventHandlerAsync(_scheduler),
                _ => throw new InvalidOperationException($"The handler type {handlerType} is not supported")
            });

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        var workflowData= new WorkflowTestData();
        workflowData.Bag["MyValue"] = "Test";
        
         _job = new Job<WorkflowTestData>(workflowData) ;
         
         var firstStep = new Sequential<WorkflowTestData>(
             "Test of Job",
            new RequestAndReactionAsync<MyCommand, MyEvent, WorkflowTestData>(
                () => new MyCommand { Value = (workflowData.Bag["MyValue"] as string)! },
                (reply) => { workflowData.Bag["MyReply"] = ((MyEvent)reply).Value; }),
             _job,
            () => { _stepCompleted = true; },
            null);
        
         InMemoryJobStoreAsync store = new();
         InMemoryJobChannel<WorkflowTestData> channel = new();
        
         _scheduler = new Scheduler<WorkflowTestData>(
             commandProcessor, 
             channel,
             store
         );
        
         _runner = new Runner<WorkflowTestData>(channel, store, commandProcessor);
    }
    
    [Fact]
    public async Task When_running_a_workflow_with_reply()
    {
        MyCommandHandlerAsync.ReceivedCommands.Clear();
        MyEventHandlerAsync.ReceivedEvents.Clear();
        
        await _scheduler.ScheduleAsync(_job);
        await _runner.RunAsync();

        _stepCompleted.Should().BeTrue();
        
        MyCommandHandlerAsync.ReceivedCommands.Any(c => c.Value == "Test").Should().BeTrue(); 
        MyEventHandlerAsync.ReceivedEvents.Any(e => e.Value == "Test").Should().BeTrue();
        _job.State.Should().Be(JobState.Done);
    }
}
