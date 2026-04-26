using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Workflows;
public class MediatorParallelSplitFlowTests
{
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly InMemoryJobChannel<WorkflowTestData> _channel;
    private readonly Job<WorkflowTestData> _job;
    private readonly WorkflowExecutionLog _executionLog = new();
    private bool _firstBranchFinished;
    private bool _secondBranchFinished;
    public MediatorParallelSplitFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        CommandProcessor commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(commandProcessor, _executionLog));
        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var workflowData = new WorkflowTestData();
        _job = new Job<WorkflowTestData>(workflowData);
        var parallelSplit = new ParallelSplit<WorkflowTestData>("Test of Job Parallel Split", (data) =>
        {
            data.Bag.TryAdd("MyValue", "Test");
            data.Bag.TryAdd("MyOtherValue", "TestTwo");
            var secondBranch = new Sequential<WorkflowTestData>("Test of Job Two", new FireAndForgetAsync<MyCommand, WorkflowTestData>((d) => new MyCommand { Value = (d.Bag["MyOtherValue"] as string)! }), () =>
            {
                _secondBranchFinished = true;
            }, null);
            var firstBranch = new Sequential<WorkflowTestData>("Test of Job One", new FireAndForgetAsync<MyCommand, WorkflowTestData>((d) => new MyCommand { Value = (d.Bag["MyValue"] as string)! }), () =>
            {
                _firstBranchFinished = true;
            }, null);
            return[firstBranch, secondBranch];
        });
        _job.InitSteps(parallelSplit);
        InMemoryStateStoreAsync store = new();
        _channel = new InMemoryJobChannel<WorkflowTestData>();
        _scheduler = new Scheduler<WorkflowTestData>(_channel, store);
        _runner = new Runner<WorkflowTestData>(_channel, store, commandProcessor, _scheduler);
    }

    [Test]
    public async Task When_running_a_workflow_with_a_parallel_split()
    {
        await _scheduler.ScheduleAsync(_job);

        var ct = new CancellationTokenSource();
        ct.CancelAfter(TimeSpan.FromSeconds(3));

        Task runnerTask = Task.CompletedTask;
        try
        {
            runnerTask = _runner.RunAsync(ct.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(200), ct.Token);
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine(e.ToString());
        }

        await Assert.That(_executionLog.Commands).Contains(c => c.Value == "Test");
        await Assert.That(_executionLog.Commands).Contains(c => c.Value == "TestTwo");
        await Assert.That(_firstBranchFinished).IsTrue();
        await Assert.That(_secondBranchFinished).IsTrue();
        await Assert.That(_job.State).IsEqualTo(JobState.Done);

        ct.Cancel();
        try { await runnerTask; }
        catch (OperationCanceledException) { }
    }
}
