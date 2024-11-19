using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorTwoStepFlowTests 
{
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly Job<WorkflowTestData> _job;

    public MediatorTwoStepFlowTests()
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
        
        var secondStep = new Sequential<WorkflowTestData>(
            "Test of Job Two",
            new FireAndForgetAsync<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (workflowData.Bag["MyValue"] as string)! }),
            () => { },
            null
            );
        
        var firstStep = new Sequential<WorkflowTestData>(
            "Test of Job One",
            new FireAndForgetAsync<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (workflowData.Bag["MyValue"] as string)! }),
            () => { workflowData.Bag["MyValue"] = "TestTwo"; }, 
            secondStep
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
        MyCommandHandlerAsync.ReceivedCommands.Clear();
        
        var ct = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try
        {
            await _scheduler.ScheduleAsync(_job);
            await _runner.RunAsync(ct.Token);
        }
        catch (TaskCanceledException)
        {
            // ignored
        }
        
        MyCommandHandlerAsync.ReceivedCommands.Any(c => c.Value == "Test").Should().BeTrue();
        MyCommandHandlerAsync.ReceivedCommands.Any(c => c.Value == "TestTwo").Should().BeTrue();
        _job.State.Should().Be(JobState.Done);
    }
}
