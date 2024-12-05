using System;
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

public class MediatorMultipleWorkflowFlowTests 
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly Job<WorkflowTestData> _firstJob;
    private readonly Job<WorkflowTestData> _secondJob;
    private bool _jobOneCompleted;
    private bool _jobTwoCompleted;

    public MediatorMultipleWorkflowFlowTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();

        CommandProcessor commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(commandProcessor));

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        PipelineBuilder<MyCommand>.ClearPipelineCache();    
        
        var firstWorkflowData= new WorkflowTestData();
        firstWorkflowData.Bag["MyValue"] = "Test";
        
        _firstJob = new Job<WorkflowTestData>(firstWorkflowData) ;
        
        var firstStep = new Sequential<WorkflowTestData>(
            "Test of Job",
            new FireAndForgetAsync<MyCommand, WorkflowTestData>((data) => 
                new MyCommand { Value = (data.Bag["MyValue"] as string)!}),
            () => { _jobOneCompleted = true; },
            null
            );
       
        _firstJob.InitSteps(firstStep);

        var secondWorkflowData = new WorkflowTestData();
        secondWorkflowData.Bag["MyValue"] = "TestTwo";
        _secondJob = new Job<WorkflowTestData>(secondWorkflowData);

        var secondStep = new Sequential<WorkflowTestData>(
            "Second Test of Job",
            new FireAndForgetAsync<MyCommand, WorkflowTestData>((data) =>
                new MyCommand { Value = (data.Bag["MyValue"] as string)! }),
            () => { _jobTwoCompleted = true; },
            null
        );
        
        InMemoryStateStoreAsync store = new();
        InMemoryJobChannel<WorkflowTestData> channel = new();
        
        _scheduler = new Scheduler<WorkflowTestData>(
            channel,
            store
        );
        
        _runner = new Runner<WorkflowTestData>(channel, store, commandProcessor, _scheduler);
    }
    
    [Fact]
    public async Task When_running_a_single_step_workflow()
    {
        MyCommandHandlerAsync.ReceivedCommands.Clear();
        
        await _scheduler.ScheduleAsync([_firstJob, _secondJob]); 
        
        var ct = new CancellationTokenSource();
        ct.CancelAfter( TimeSpan.FromSeconds(120) );

        try
        {
            _runner.RunAsync(ct.Token);
        }
        catch (Exception e)
        {
            _testOutputHelper.WriteLine(e.ToString());
        }
        
        MyCommandHandlerAsync.ReceivedCommands.Any(c => c.Value == "Test").Should().BeTrue();
        MyCommandHandlerAsync.ReceivedCommands.Any(c => c.Value == "TestTwo").Should().BeTrue();
        _firstJob.State.Should().Be(JobState.Done);
        _secondJob.State.Should().Be(JobState.Done);
        _jobOneCompleted.Should().BeTrue();
        _jobTwoCompleted.Should().BeTrue();
    }
}
