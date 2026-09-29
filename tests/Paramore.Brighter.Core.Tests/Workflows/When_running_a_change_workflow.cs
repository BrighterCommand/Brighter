using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Workflows;
public class MediatorChangeStepFlowTests
{
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly InMemoryJobChannel<WorkflowTestData> _channel;
    private readonly Job<WorkflowTestData> _job;
    private readonly WorkflowExecutionLog _executionLog = new();
    private bool _stepCompleted;
    public MediatorChangeStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        CommandProcessor? commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(commandProcessor, _executionLog));
        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var workflowData = new WorkflowTestData
        {
            Bag =
            {
                ["MyValue"] = "Test"
            }
        };
        _job = new Job<WorkflowTestData>(workflowData);
        var firstStep = new Sequential<WorkflowTestData>("Test of Job", new ChangeAsync<WorkflowTestData>((data) =>
        {
            var tcs = new TaskCompletionSource();
            data.Bag["MyValue"] = "Altered";
            tcs.SetResult();
            return tcs.Task;
        }), () =>
        {
            _stepCompleted = true;
        }, null);
        _job.InitSteps(firstStep);
        var store = new InMemoryStateStoreAsync();
        _channel = new InMemoryJobChannel<WorkflowTestData>();
        _scheduler = new Scheduler<WorkflowTestData>(_channel, store);
        _runner = new Runner<WorkflowTestData>(_channel, store, commandProcessor, _scheduler);
    }

    [Test]
    public async Task When_running_a_change_workflow()
    {
        await _scheduler.ScheduleAsync(_job);
        _channel.Stop();

        var ct = new CancellationTokenSource();
        ct.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            await _runner.RunAsync(ct.Token);
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine(ex.ToString());
        }

        await Assert.That(_job.State).IsEqualTo(JobState.Done);
        await Assert.That(_stepCompleted).IsTrue();
        await Assert.That(_job.Data.Bag["MyValue"]).IsEqualTo("Altered");
    }
}
