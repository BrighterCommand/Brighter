using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using MyCommand = Paramore.Brighter.Core.Tests.Workflows.TestDoubles.MyCommand;
using MyCommandHandlerAsync = Paramore.Brighter.Core.Tests.Workflows.TestDoubles.MyCommandHandlerAsync;
using MyEvent = Paramore.Brighter.Core.Tests.Workflows.TestDoubles.MyEvent;
using MyEventHandlerAsync = Paramore.Brighter.Core.Tests.Workflows.TestDoubles.MyEventHandlerAsync;

namespace Paramore.Brighter.Core.Tests.Workflows;
public class MediatorReplyStepFlowTests
{
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly InMemoryJobChannel<WorkflowTestData> _channel;
    private readonly Job<WorkflowTestData> _job;
    private readonly WorkflowExecutionLog _executionLog = new();
    private bool _stepCompleted;
    public MediatorReplyStepFlowTests()
    {
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        registry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
        IAmACommandProcessor? commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync((handlerType) => handlerType switch
        {
            _ when handlerType == typeof(MyCommandHandlerAsync) => new MyCommandHandlerAsync(commandProcessor, _executionLog),
            _ when handlerType == typeof(MyEventHandlerAsync) => new MyEventHandlerAsync(_scheduler, _executionLog),
            _ => throw new InvalidOperationException($"The handler type {handlerType} is not supported")});
        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var workflowData = new WorkflowTestData();
        workflowData.Bag["MyValue"] = "Test";
        _job = new Job<WorkflowTestData>(workflowData);
        var firstStep = new Sequential<WorkflowTestData>("Test of Job", new RequestAndReactionAsync<MyCommand, MyEvent, WorkflowTestData>((data) => new MyCommand { Value = (data.Bag["MyValue"] as string)! }, (reply, data) =>
        {
            data.Bag["MyReply"] = reply!.Value;
        }), () =>
        {
            _stepCompleted = true;
        }, null);
        _job.InitSteps(firstStep);
        InMemoryStateStoreAsync store = new();
        _channel = new InMemoryJobChannel<WorkflowTestData>();
        _scheduler = new Scheduler<WorkflowTestData>(_channel, store);
        _runner = new Runner<WorkflowTestData>(_channel, store, commandProcessor, _scheduler);
    }

    [Test]
    public async Task When_running_a_workflow_with_reply()
    {
        await _scheduler.ScheduleAsync(_job);
        _channel.Stop();

        var ct = new CancellationTokenSource();
        ct.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            await _runner.RunAsync(ct.Token);
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine(e.ToString());
        }

        await Assert.That(_stepCompleted).IsTrue();
        await Assert.That(_executionLog.Commands).Contains(c => c.Value == "Test");
        await Assert.That(_executionLog.Events).Contains(e => e.Value == "Test");
        await Assert.That(_job.State).IsEqualTo(JobState.Done);
    }
}
