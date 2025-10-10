﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorRobustReplyFaultStepFlowTests  
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly Job<WorkflowTestData> _job;
    private bool _stepCompleted;
    private bool _stepFaulted;

    public MediatorRobustReplyFaultStepFlowTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        registry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
        registry.RegisterAsync<MyFault, MyFaultHandlerAsync>();

        IAmACommandProcessor commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync((handlerType) =>
             handlerType switch
            { 
                _ when handlerType == typeof(MyCommandHandlerAsync) => new MyCommandHandlerAsync(commandProcessor, raiseFault: true),
                _ when handlerType == typeof(MyEventHandlerAsync) => new MyEventHandlerAsync(_scheduler),
                _ when handlerType == typeof(MyFaultHandlerAsync) => new MyFaultHandlerAsync(_scheduler),
                _ => throw new InvalidOperationException($"The handler type {handlerType} is not supported")
            });

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
            new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory());
        
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        var workflowData= new WorkflowTestData();
        workflowData.Bag["MyValue"] = "Test";
        
         _job = new Job<WorkflowTestData>(workflowData) ;
         
         var firstStep = new Sequential<WorkflowTestData>(
             "Test of Job",
            new RobustRequestAndReactionAsync<MyCommand, MyEvent, MyFault, WorkflowTestData>(
                (data) => new MyCommand { Value = (data.Bag["MyValue"] as string)! },
                (reply, data) => { data.Bag["MyReply"] = reply!.Value; },
            (fault, data) => { data.Bag["MyFault"] = fault!.Value; }),
            () => { _stepCompleted = true; },
            null,
            () => { _stepFaulted = true; },
         null);
         
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
    public async Task When_running_a_workflow_with_reply()
    {
        MyCommandHandlerAsync.ReceivedCommands.Clear();
        MyEventHandlerAsync.ReceivedEvents.Clear();
        MyFaultHandlerAsync.ReceivedFaults.Clear();
        
        await _scheduler.ScheduleAsync(_job);

        
        var ct = new CancellationTokenSource();
        ct.CancelAfter( TimeSpan.FromSeconds(1) );

        try
        {
            _runner.RunAsync(ct.Token);
        }
        catch (Exception e)
        {
            _testOutputHelper.WriteLine(e.ToString());
        }
        
        Assert.Contains(MyCommandHandlerAsync.ReceivedCommands, c => c.Value == "Test");
        Assert.Contains(MyFaultHandlerAsync.ReceivedFaults, e => e.Value == "Test");
        Assert.Empty(MyEventHandlerAsync.ReceivedEvents);
        Assert.Equal("Test", _job.Data.Bag["MyValue"]);
        Assert.Equal("Test", _job.Data.Bag["MyFault"]);
        Assert.Equal(JobState.Done, _job.State);
        Assert.True(_stepCompleted);
        Assert.False(_stepFaulted);
    }
}
