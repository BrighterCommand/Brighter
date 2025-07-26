using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorChangeStepFlowTests 
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly Job<WorkflowTestData> _job;
    private bool _stepCompleted;

    public MediatorChangeStepFlowTests (ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();

        CommandProcessor? commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(commandProcessor));

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
            new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory());
        PipelineBuilder<MyCommand>.ClearPipelineCache();    
        
        var workflowData= new WorkflowTestData { Bag = { ["MyValue"] = "Test" } };

        _job = new Job<WorkflowTestData>(workflowData) ;
        
        var firstStep = new Sequential<WorkflowTestData>(
            "Test of Job",
            new ChangeAsync<WorkflowTestData>( (data) =>
            {
                var tcs = new TaskCompletionSource();
                data.Bag["MyValue"] = "Altered";
                tcs.SetResult();
                return tcs.Task;
            }),
            () => { _stepCompleted = true; },
            null
            );
        
        _job.InitSteps(firstStep);
        
        var store = new InMemoryStateStoreAsync ();
        InMemoryJobChannel<WorkflowTestData> channel = new();
        
        _scheduler = new Scheduler<WorkflowTestData>(
            channel,
            store
        );
        
        _runner = new Runner<WorkflowTestData>(channel, store, commandProcessor, _scheduler);
    }
    
    [Fact]
    public async Task When_running_a_change_workflow()
    {
        await _scheduler.ScheduleAsync(_job);

        //let it run long enough to finish work, then terminate
        var ct = new CancellationTokenSource();
        ct.CancelAfter( TimeSpan.FromSeconds(1) );
        try
        {
            _runner.RunAsync(ct.Token);
        }
        catch (Exception ex)
        {
            // swallow the exception, we expect it to be cancelled
            _testOutputHelper.WriteLine(ex.ToString());
        }
        
        Assert.Equal(JobState.Done, _job.State);
        Assert.True(_stepCompleted);
        Assert.Equal("Altered", _job.Data.Bag["MyValue"]);
    }
}
