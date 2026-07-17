using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Workflows;
public class MediatorReplyMultiStepFlowTests
{
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly InMemoryJobChannel<WorkflowTestData> _channel;
    private readonly Job<WorkflowTestData> _job;
    private readonly WorkflowExecutionLog _executionLog = new();
    private bool _stepCompletedOne;
    private bool _stepCompletedTwo;
    public MediatorReplyMultiStepFlowTests()
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
        var stepTwo = new Sequential<WorkflowTestData>("Test of Job SequenceStep Two", new FireAndForgetAsync<MyCommand, WorkflowTestData>((data) => new MyCommand { Value = (data.Bag["MyValue"] as string)! }), () =>
        {
            _stepCompletedTwo = true;
        }, null);
        Sequential<WorkflowTestData> stepOne = new("Test of Job SequenceStep One", new RequestAndReactionAsync<MyCommand, MyEvent, WorkflowTestData>((data) => new MyCommand { Value = (data.Bag["MyValue"] as string)! }, (reply, data) => data.Bag["MyReply"] = ((MyEvent)reply).Value), () =>
        {
            _stepCompletedOne = true;
        }, stepTwo);
        _job.InitSteps(stepOne);
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
        ct.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            await _runner.RunAsync(ct.Token);
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine(e.ToString());
        }

        await Assert.That(_stepCompletedOne).IsTrue();
        await Assert.That(_stepCompletedTwo).IsTrue();
        await Assert.That((_executionLog.Commands).Any(c => c.Value == "Test")).IsTrue();
        await Assert.That((_executionLog.Events).Any(e => e.Value == "Test")).IsTrue();
        await Assert.That(_job.State).IsEqualTo(JobState.Done);
    }
}
