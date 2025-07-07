﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorReplyMultiStepFlowTests  
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly Job<WorkflowTestData> _job;
    private bool _stepCompletedOne;
    private bool _stepCompletedTwo;

    public MediatorReplyMultiStepFlowTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        registry.RegisterAsync<MyEvent, MyEventHandlerAsync>();

        IAmACommandProcessor? commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync((handlerType) =>
             handlerType switch
            { 
                _ when handlerType == typeof(MyCommandHandlerAsync) => new MyCommandHandlerAsync(commandProcessor),
                _ when handlerType == typeof(MyEventHandlerAsync) => new MyEventHandlerAsync(_scheduler),
                _ => throw new InvalidOperationException($"The handler type {handlerType} is not supported")
            });

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new InMemorySchedulerFactory());
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        var workflowData= new WorkflowTestData();
        workflowData.Bag["MyValue"] = "Test";

        _job = new Job<WorkflowTestData>(workflowData) ;
        
        var stepTwo = new Sequential<WorkflowTestData>(
            "Test of Job SequenceStep Two",
            new FireAndForgetAsync<MyCommand, WorkflowTestData>((data) => 
                new MyCommand { Value = (data.Bag["MyValue"] as string)! }),
            () => { _stepCompletedTwo = true; },
            null);
        
         Sequential<WorkflowTestData> stepOne = new(
             "Test of Job SequenceStep One",
            new RequestAndReactionAsync<MyCommand, MyEvent, WorkflowTestData>(
                (data) => new MyCommand { Value = (data.Bag["MyValue"] as string)! },
                (reply, data) => data.Bag["MyReply"] = ((MyEvent)reply).Value),
            () => { _stepCompletedOne = true; },
            stepTwo);
         
         _job.InitSteps(stepOne); 
        
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
        
        Assert.True(_stepCompletedOne);
        Assert.True(_stepCompletedTwo);

        Assert.Contains(MyCommandHandlerAsync.ReceivedCommands, c => c.Value == "Test");
        Assert.Contains(MyEventHandlerAsync.ReceivedEvents, e => e.Value == "Test");
        Assert.Equal(JobState.Done, _job.State);
    }
}
