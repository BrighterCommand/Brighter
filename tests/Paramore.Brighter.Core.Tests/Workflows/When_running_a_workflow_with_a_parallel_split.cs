using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorParallelSplitFlowTests 
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly Job<WorkflowTestData> _job;
    private bool _firstBranchFinished;
    private bool _secondBranchFinished;

    public MediatorParallelSplitFlowTests(ITestOutputHelper testOutputHelper)
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
        
        _job = new Job<WorkflowTestData>(workflowData) ;
        
        var parallelSplit = new ParallelSplit<WorkflowTestData>(
            "Test of Job Parallel Split",
            (data) =>
            {   
                data.Bag.TryAdd("MyValue", "Test");
                data.Bag.TryAdd("MyOtherValue", "TestTwo");
                
                var secondBranch = new Sequential<WorkflowTestData>(
                    "Test of Job Two",
                    new FireAndForgetAsync<MyCommand, WorkflowTestData>((d) => 
                        new MyCommand { Value = (d.Bag["MyOtherValue"] as string)! }),
                    () => { _secondBranchFinished = true; },
                    null
                );
        
                var firstBranch = new Sequential<WorkflowTestData>(
                    "Test of Job One",
                    new FireAndForgetAsync<MyCommand, WorkflowTestData>((d) => 
                        new MyCommand { Value = (d.Bag["MyValue"] as string)! }),
                    () => {  _firstBranchFinished = true;  }, 
                    null
                );
                
                return [firstBranch, secondBranch];
            }
        );
        
        _job.InitSteps(parallelSplit);
        
        InMemoryStateStoreAsync store = new();
        InMemoryJobChannel<WorkflowTestData> channel = new();
        
        _scheduler = new Scheduler<WorkflowTestData>(
            channel,
            store
        );
        
        _runner = new Runner<WorkflowTestData>(channel, store, commandProcessor, _scheduler);
    }
    
    [Fact]
    public async  Task When_running_a_workflow_with_a_parallel_split()
    {
        MyCommandHandlerAsync.ReceivedCommands.Clear();
        
        await _scheduler.ScheduleAsync(_job);
        
        var ct = new CancellationTokenSource();
        ct.CancelAfter( TimeSpan.FromSeconds(3) );

        try
        {
            _runner.RunAsync(ct.Token);
        }
        catch (Exception e)
        {
            _testOutputHelper.WriteLine(e.ToString());
        }
        
        Assert.Contains(MyCommandHandlerAsync.ReceivedCommands, c => c.Value == "Test");
        Assert.Contains(MyCommandHandlerAsync.ReceivedCommands, c => c.Value == "TestTwo");
        Assert.True(_firstBranchFinished);
        Assert.True(_secondBranchFinished);
        Assert.Equal(JobState.Done, _job.State);
    }
}
