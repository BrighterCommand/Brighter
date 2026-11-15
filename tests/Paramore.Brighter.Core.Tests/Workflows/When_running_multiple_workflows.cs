using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Workflows;
public class MediatorMultipleWorkflowFlowTests
{
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly InMemoryJobChannel<WorkflowTestData> _channel;
    private readonly Job<WorkflowTestData> _firstJob;
    private readonly Job<WorkflowTestData> _secondJob;
    private readonly WorkflowExecutionLog _executionLog = new();
    private bool _jobOneCompleted;
    private bool _jobTwoCompleted;
    public MediatorMultipleWorkflowFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        CommandProcessor? commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(commandProcessor, _executionLog));
        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var firstWorkflowData = new WorkflowTestData
        {
            Bag =
            {
                ["MyValue"] = "Test"
            }
        };
        _firstJob = new Job<WorkflowTestData>(firstWorkflowData);
        var firstStep = new Sequential<WorkflowTestData>("Test of Job", new FireAndForgetAsync<MyCommand, WorkflowTestData>((data) => new MyCommand { Value = (data.Bag["MyValue"] as string)! }), () =>
        {
            _jobOneCompleted = true;
        }, null);
        _firstJob.InitSteps(firstStep);
        var secondWorkflowData = new WorkflowTestData();
        secondWorkflowData.Bag["MyValue"] = "TestTwo";
        _secondJob = new Job<WorkflowTestData>(secondWorkflowData);
        var secondStep = new Sequential<WorkflowTestData>("Second Test of Job", new FireAndForgetAsync<MyCommand, WorkflowTestData>((data) => new MyCommand { Value = (data.Bag["MyValue"] as string)! }), () =>
        {
            _jobTwoCompleted = true;
        }, null);
        _secondJob.InitSteps(secondStep);
        InMemoryStateStoreAsync store = new();
        _channel = new InMemoryJobChannel<WorkflowTestData>();
        _scheduler = new Scheduler<WorkflowTestData>(_channel, store);
        _runner = new Runner<WorkflowTestData>(_channel, store, commandProcessor, _scheduler);
    }

    [Test]
    public async Task When_running_a_single_step_workflow()
    {
        await _scheduler.ScheduleAsync([_firstJob, _secondJob]);
        _channel.Stop();

        var ct = new CancellationTokenSource();
        ct.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await _runner.RunAsync(ct.Token);
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine(e.ToString());
        }

        await Assert.That((_executionLog.Commands).Any(c => c.Value == "Test")).IsTrue();
        await Assert.That((_executionLog.Commands).Any(c => c.Value == "TestTwo")).IsTrue();
        await Assert.That(_firstJob.State).IsEqualTo(JobState.Done);
        await Assert.That(_secondJob.State).IsEqualTo(JobState.Done);
        await Assert.That(_jobOneCompleted).IsTrue();
        await Assert.That(_jobTwoCompleted).IsTrue();
    }
}
