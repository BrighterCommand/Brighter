using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Workflows;
public class MediatorTwoStepFlowTests
{
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly InMemoryJobChannel<WorkflowTestData> _channel;
    private readonly Job<WorkflowTestData> _job;
    private readonly WorkflowExecutionLog _executionLog = new();
    private bool _stepsCompleted;
    public MediatorTwoStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        CommandProcessor commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(commandProcessor, _executionLog));
        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var workflowData = new WorkflowTestData();
        workflowData.Bag["MyValue"] = "Test";
        _job = new Job<WorkflowTestData>(workflowData);
        var secondStep = new Sequential<WorkflowTestData>("Test of Job Two", new FireAndForgetAsync<MyCommand, WorkflowTestData>((data) => new MyCommand { Value = (data.Bag["MyValue"] as string)! }), () =>
        {
            _stepsCompleted = true;
        }, null);
        var firstStep = new Sequential<WorkflowTestData>("Test of Job One", new FireAndForgetAsync<MyCommand, WorkflowTestData>((data) => new MyCommand { Value = (data.Bag["MyValue"] as string)! }), () =>
        {
            workflowData.Bag["MyValue"] = "TestTwo";
        }, secondStep);
        _job.InitSteps(firstStep);
        InMemoryStateStoreAsync store = new();
        _channel = new InMemoryJobChannel<WorkflowTestData>();
        _scheduler = new Scheduler<WorkflowTestData>(_channel, store);
        _runner = new Runner<WorkflowTestData>(_channel, store, commandProcessor, _scheduler);
    }

    [Test]
    public async Task When_running_a_two_step_workflow()
    {
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
            Console.WriteLine(e.ToString());
        }

        await Assert.That((_executionLog.Commands).Any(c => c.Value == "Test")).IsTrue();
        await Assert.That((_executionLog.Commands).Any(c => c.Value == "TestTwo")).IsTrue();
        await Assert.That(_job.State).IsEqualTo(JobState.Done);
        await Assert.That(_stepsCompleted).IsTrue();
    }
}
