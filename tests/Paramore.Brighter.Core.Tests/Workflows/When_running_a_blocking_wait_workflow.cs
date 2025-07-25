using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorWaitStepFlowTests 
{
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly Job<WorkflowTestData> _job;
    private bool _stepCompleted;
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly Waker<WorkflowTestData> _waker;

    public MediatorWaitStepFlowTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();

        CommandProcessor commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(commandProcessor));
        
        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
            new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory());
        PipelineBuilder<MyCommand>.ClearPipelineCache();    
        
        var workflowData= new WorkflowTestData();
        workflowData.Bag["MyValue"] = "Test";
        
        _job = new Job<WorkflowTestData>(workflowData);
        
        var secondStep = new Sequential<WorkflowTestData>(
            "Test of Job",
            new ChangeAsync<WorkflowTestData>( (_) => Task.CompletedTask),
            () => { _stepCompleted = true; },
            null
        );
        
        var firstStep = new Wait<WorkflowTestData>("Test of Job",
            TimeSpan.FromMilliseconds(100),
            secondStep
            );
        
        _job.InitSteps(firstStep);

        InMemoryStateStoreAsync store = new(_timeProvider);
        InMemoryJobChannel<WorkflowTestData> channel = new();
        
        _scheduler = new Scheduler<WorkflowTestData>(
            channel,
            store
            );
        
        _runner = new Runner<WorkflowTestData>(channel, store, commandProcessor, _scheduler);
        _waker = new Waker<WorkflowTestData>(TimeSpan.FromMilliseconds(100), _scheduler);
    }
    
    [Fact]
    public async Task When_running_a_wait_workflow()
    {
        var ct = new CancellationTokenSource();
        ct.CancelAfter( TimeSpan.FromSeconds(3));

        try
        {
            await _scheduler.ScheduleAsync(_job);
            
            _timeProvider.Advance(TimeSpan.FromMilliseconds(1000));
            
            _runner.RunAsync(ct.Token);
            _waker.RunAsync(ct.Token);

            await Task.Delay(5, ct.Token);

        }
        catch (Exception e)
        {
            _testOutputHelper.WriteLine(e.ToString());
        }

        Assert.True(_stepCompleted);
        Assert.Equal(JobState.Done, _job.State);
    }
}
