using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class MediatorOneStepFlowTests 
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly InMemoryJobChannel<WorkflowTestData> _channel;
    private readonly Job<WorkflowTestData> _job;
    private bool _stepCompleted;

    public MediatorOneStepFlowTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();

        CommandProcessor commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(commandProcessor));

        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
            new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory(loggerFactory: Initializer.TestLoggerFactory), loggerFactory: Initializer.TestLoggerFactory);
        PipelineBuilder<MyCommand>.ClearPipelineCache();    
        
        var workflowData= new WorkflowTestData();
        workflowData.Bag["MyValue"] = "Test";
        
        _job = new Job<WorkflowTestData>(workflowData) ;
        
        var firstStep = new Sequential<WorkflowTestData>(
            "Test of Job",
            new FireAndForgetAsync<MyCommand, WorkflowTestData>((data) => 
                new MyCommand { Value = (workflowData.Bag["MyValue"] as string)!}),
            () => { _stepCompleted = true; },
            null,
            loggerFactory: Initializer.TestLoggerFactory);
       
        _job.InitSteps(firstStep);
        
        InMemoryStateStoreAsync store = new(loggerFactory: Initializer.TestLoggerFactory);
        _channel = new InMemoryJobChannel<WorkflowTestData>(loggerFactory: Initializer.TestLoggerFactory);

        _scheduler = new Scheduler<WorkflowTestData>(
            _channel,
            store
        );

        _runner = new Runner<WorkflowTestData>(_channel, store, commandProcessor, _scheduler, loggerFactory: Initializer.TestLoggerFactory);
    }
    
    [Fact]
    public async Task When_running_a_single_step_workflow()
    {
        MyCommandHandlerAsync.ReceivedCommands.Clear();
        
        await _scheduler.ScheduleAsync(_job);
        _channel.Stop();

        var ct = new CancellationTokenSource();
        ct.CancelAfter(TimeSpan.FromSeconds(1));

        try
        {
            await _runner.RunAsync(ct.Token);
        }
        catch (OperationCanceledException e)
        {
            _testOutputHelper.WriteLine(e.ToString());
        }

        Assert.Contains(MyCommandHandlerAsync.ReceivedCommands, c => c.Value == "Test");
        Assert.Equal(JobState.Done, _job.State);
        Assert.True(_stepCompleted);
    }
}
