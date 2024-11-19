using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorBlockingWaitStepFlowTests 
{
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly Job<WorkflowTestData> _job;
    private bool _stepCompleted;

    public MediatorBlockingWaitStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();

        CommandProcessor commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(commandProcessor));

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();    
        
        var workflowData= new WorkflowTestData();
        workflowData.Bag["MyValue"] = "Test";
        
        _job = new Job<WorkflowTestData>(workflowData) ;
        
        var firstStep = new Wait<WorkflowTestData>("Test of Job",
            TimeSpan.FromMilliseconds(500),
            () => { _stepCompleted = true; },
            null
            );
        
        _job.InitSteps(firstStep); 
        
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
    public async Task When_running_a_single_step_workflow()
    {
        await _scheduler.ScheduleAsync(_job);
        
        //We won't really see th block in action as the test will simply block for 500ms
        await _runner.RunAsync();
        
        _job.State.Should().Be(JobState.Done);
        _stepCompleted.Should().BeTrue();
    }
}
