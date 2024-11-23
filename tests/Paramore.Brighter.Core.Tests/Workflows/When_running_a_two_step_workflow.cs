﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorTwoStepFlowTests 
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly Job<WorkflowTestData> _job;
    private bool _stepsCompleted;

    public MediatorTwoStepFlowTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
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
            () => { _stepsCompleted = true; },
            null
            );
        
        var firstStep = new Sequential<WorkflowTestData>(
            "Test of Job One",
            new FireAndForgetAsync<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (workflowData.Bag["MyValue"] as string)! }),
            () => { workflowData.Bag["MyValue"] = "TestTwo"; }, 
            secondStep
            );
        
        _job.InitSteps(firstStep); 
        
        InMemoryStateStoreAsync store = new();
        InMemoryJobChannel<WorkflowTestData> channel = new();
        
        _scheduler = new Scheduler<WorkflowTestData>(
            channel,
            store
        );
        
        _runner = new Runner<WorkflowTestData>(channel, store, commandProcessor, _scheduler);
    }
    
    [Fact]
    public async Task When_running_a_two_step_workflow()
    {
        MyCommandHandlerAsync.ReceivedCommands.Clear();
        await _scheduler.ScheduleAsync(_job);
        
        var ct = new CancellationTokenSource();
        ct.CancelAfter( TimeSpan.FromSeconds(1) );

        try
        {
            await _runner.RunAsync(ct.Token);
        }
        catch (Exception e)
        {
            _testOutputHelper.WriteLine(e.ToString());
        }
        
        MyCommandHandlerAsync.ReceivedCommands.Any(c => c.Value == "Test").Should().BeTrue();
        MyCommandHandlerAsync.ReceivedCommands.Any(c => c.Value == "TestTwo").Should().BeTrue();
        _job.State.Should().Be(JobState.Done);
        _stepsCompleted.Should().BeTrue();
    }
}
