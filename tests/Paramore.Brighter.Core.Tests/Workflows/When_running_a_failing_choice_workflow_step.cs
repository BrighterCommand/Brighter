using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Workflows;
public class MediatorFailingChoiceFlowTests
{
    private readonly Scheduler<WorkflowTestData> _scheduler;
    private readonly Runner<WorkflowTestData> _runner;
    private readonly InMemoryJobChannel<WorkflowTestData> _channel;
    private readonly Job<WorkflowTestData> _job;
    private readonly WorkflowExecutionLog _executionLog = new();
    private bool _stepCompletedOne;
    private bool _stepCompletedTwo;
    private bool _stepCompletedThree;
    public MediatorFailingChoiceFlowTests()
    {
        // arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        registry.RegisterAsync<MyOtherCommand, MyOtherCommandHandlerAsync>();
        IAmACommandProcessor? commandProcessor = null;
        var handlerFactory = new SimpleHandlerFactoryAsync((handlerType) => handlerType switch
        {
            _ when handlerType == typeof(MyCommandHandlerAsync) => new MyCommandHandlerAsync(commandProcessor, _executionLog),
            _ when handlerType == typeof(MyOtherCommandHandlerAsync) => new MyOtherCommandHandlerAsync(commandProcessor, _executionLog),
            _ => throw new InvalidOperationException($"The handler type {handlerType} is not supported")});
        commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var workflowData = new WorkflowTestData();
        workflowData.Bag["MyValue"] = "Fail";
        _job = new Job<WorkflowTestData>(workflowData);
        var stepThree = new Sequential<WorkflowTestData>("Test of Job SequenceStep Three", new FireAndForgetAsync<MyOtherCommand, WorkflowTestData>((data) => new MyOtherCommand { Value = (data.Bag["MyValue"] as string)! }), () =>
        {
            _stepCompletedThree = true;
        }, null);
        var stepTwo = new Sequential<WorkflowTestData>("Test of Job SequenceStep Two", new FireAndForgetAsync<MyCommand, WorkflowTestData>((data) => new MyCommand { Value = (data.Bag["MyValue"] as string)! }), () =>
        {
            _stepCompletedTwo = true;
        }, null);
        var stepOne = new ExclusiveChoice<WorkflowTestData>("Test of Job SequenceStep One", new Specification<WorkflowTestData>(data => data.Bag["MyValue"] as string == "Pass"), () =>
        {
            _stepCompletedOne = true;
        }, stepTwo, stepThree);
        _job.InitSteps(stepOne);
        InMemoryStateStoreAsync store = new();
        _channel = new InMemoryJobChannel<WorkflowTestData>();
        _scheduler = new Scheduler<WorkflowTestData>(_channel, store);
        _runner = new Runner<WorkflowTestData>(_channel, store, commandProcessor, _scheduler);
    }

    [Test]
    public async Task When_running_a_choice_workflow_step()
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

        // Assert
        await Assert.That(_stepCompletedOne).IsTrue();
        await Assert.That(_stepCompletedTwo).IsFalse();
        await Assert.That(_stepCompletedThree).IsTrue();
        await Assert.That(_executionLog.OtherCommands).Contains(c => c.Value == "Fail");
        await Assert.That(_executionLog.Commands.Any()).IsFalse();
    }
}
