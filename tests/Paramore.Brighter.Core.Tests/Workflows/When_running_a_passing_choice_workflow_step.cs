﻿using System;
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

public class MediatorPassingChoiceFlowTests 
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly Job<WorkflowTestData> _job;
    private bool _stepCompletedOne;
    private bool _stepCompletedTwo;
    private bool _stepCompletedThree;

    public MediatorPassingChoiceFlowTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        // arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        registry.RegisterAsync<MyOtherCommand, MyOtherCommandHandlerAsync>();

        IAmACommandProcessor? commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync((handlerType) =>
            handlerType switch
            { 
                _ when handlerType == typeof(MyCommandHandlerAsync) => new MyCommandHandlerAsync(commandProcessor),
                _ when handlerType == typeof(MyOtherCommandHandlerAsync) => new (commandProcessor),
                _ => throw new InvalidOperationException($"The handler type {handlerType} is not supported")
            });

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        var workflowData= new WorkflowTestData();
        workflowData.Bag["MyValue"] = "Pass";
        
        _job = new Job<WorkflowTestData>(workflowData) ;
        
        var stepThree = new Sequential<WorkflowTestData>(
            "Test of Job SequenceStep Three",
            new FireAndForgetAsync<MyOtherCommand, WorkflowTestData>(() => new MyOtherCommand { Value = (workflowData.Bag["MyValue"] as string)! }),
            () => { _stepCompletedThree = true; },
            null);
        
        var stepTwo = new Sequential<WorkflowTestData>(
            "Test of Job SequenceStep Two",
            new FireAndForgetAsync<MyCommand, WorkflowTestData>(() => new MyCommand { Value = (workflowData.Bag["MyValue"] as string)! }),
            () => { _stepCompletedTwo = true; },
            null);

         var stepOne = new ExclusiveChoice<WorkflowTestData>(
             "Test of Job SequenceStep One",
             new Specification<WorkflowTestData>(x => x.Bag["MyValue"] as string == "Pass"),
            () => { _stepCompletedOne = true; },
            stepTwo, 
            stepThree);
         
         _job.InitSteps(stepOne); 
        
        InMemoryStateStoreAsync store = new();
        InMemoryJobChannel<WorkflowTestData> channel = new();
        
        _scheduler = new Scheduler<WorkflowTestData>(
            commandProcessor, 
            channel,
            store
        );
        
        _runner = new Runner<WorkflowTestData>(channel, store, commandProcessor);
    }
    
    [Fact]
    public async Task When_running_a_choice_workflow_step()
    {
        MyCommandHandlerAsync.ReceivedCommands.Clear();
        MyOtherCommandHandlerAsync.ReceivedCommands.Clear();
        
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
        _stepCompletedOne.Should().BeTrue();
        _stepCompletedTwo.Should().BeTrue();
        _stepCompletedThree.Should().BeFalse();
        MyCommandHandlerAsync.ReceivedCommands.Any(c => c.Value == "Pass").Should().BeTrue();
        MyOtherCommandHandlerAsync.ReceivedCommands.Any().Should().BeFalse();
        _stepCompletedOne.Should().BeTrue();
    }
}
